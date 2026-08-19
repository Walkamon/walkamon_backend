using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BLL.Exceptions;
using BLL.Interfaces;
using BLL.Options;
using DAL.Data;
using DAL.DTO;
using DAL.Extensions;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BLL.Service;

public sealed class ValidatedStepService : IValidatedStepService
{
    private const long MotionHistoryRetentionNs = 135_000_000_000L;
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    private readonly WalkamonContext _context;
    private readonly IAppAttestationVerifier _attestationVerifier;
    private readonly IAchievementProgressService _achievementProgressService;
    private readonly IMissionProgressService _missionProgressService;
    private readonly StepValidationOptions _options;
    private readonly MotionValidationOptions _motionOptions;
    private readonly TimePresentationSerializer _timePresentationSerializer;
    private readonly ILogger<ValidatedStepService> _logger;
    private readonly IStepTrackingBenchmarkSink _benchmarkSink;

    public ValidatedStepService(
        WalkamonContext context,
        IAppAttestationVerifier attestationVerifier,
        IAchievementProgressService achievementProgressService,
        IMissionProgressService missionProgressService,
        IOptions<StepValidationOptions> options,
        IOptions<MotionValidationOptions> motionOptions,
        TimePresentationSerializer? timePresentationSerializer = null,
        ILogger<ValidatedStepService>? logger = null,
        IStepTrackingBenchmarkSink? benchmarkSink = null)
    {
        _context = context;
        _attestationVerifier = attestationVerifier;
        _achievementProgressService = achievementProgressService;
        _missionProgressService = missionProgressService;
        _options = options.Value;
        StepValidationConfigurationValidator.Validate(_options);
        _motionOptions = motionOptions.Value;
        _timePresentationSerializer = timePresentationSerializer
            ?? new TimePresentationSerializer(
                Microsoft.Extensions.Options.Options.Create(new TimePresentationOptions()));
        _logger = logger ?? NullLogger<ValidatedStepService>.Instance;
        _benchmarkSink = benchmarkSink ?? NullStepTrackingBenchmarkSink.Instance;
    }

    public Task<PvpStepSessionResponse> CreateDailySessionAsync(
        Guid userId,
        CreatePvpStepSessionRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSessionAsync(userId, null, "daily", request, cancellationToken);

    public Task<PvpStepSessionResponse> CreatePvpSessionAsync(
        Guid userId,
        Guid matchId,
        CreatePvpStepSessionRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSessionAsync(userId, matchId, "pvp", request, cancellationToken);

    public Task<PvpStepBatchResponse> SubmitDailyBatchAsync(
        Guid userId,
        Guid sessionId,
        SubmitPvpStepBatchRequest request,
        CancellationToken cancellationToken = default) =>
        SubmitBatchAsync(userId, null, sessionId, "daily", request, cancellationToken);

    public Task<PvpStepBatchResponse> SubmitPvpBatchAsync(
        Guid userId,
        Guid matchId,
        Guid sessionId,
        SubmitPvpStepBatchRequest request,
        CancellationToken cancellationToken = default) =>
        SubmitBatchAsync(userId, matchId, sessionId, "pvp", request, cancellationToken);

    private async Task<PvpStepSessionResponse> CreateSessionAsync(
        Guid userId,
        Guid? matchId,
        string purposeCode,
        CreatePvpStepSessionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateSessionRequest(request);
        return await _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
        {
        var now = DateTime.UtcNow;
        if (!await _context.Users.AnyAsync(
                x => x.UserId == userId && x.StatusCode == "active" && x.DeletedAt == null,
                cancellationToken))
            throw new ForbiddenException("User is unavailable for physical-step validation.");

        PvpMatch? match = null;
        if (purposeCode == "daily")
        {
            var blocked = await _context.PvpPlayerActivities.AnyAsync(
                x => x.UserId == userId &&
                     (x.ActivityType == "match_countdown" ||
                      x.ActivityType == "match_running" ||
                      x.ActivityType == "match_settling"),
                cancellationToken);
            if (blocked) throw new ConflictException("Daily step session is paused during an active PvP match.");
        }
        else
        {
            match = await _context.PvpMatches
                .Include(x => x.PvpMatchPlayers)
                .FirstOrDefaultAsync(x => x.MatchId == matchId, cancellationToken)
                ?? throw new NotFoundException("Sprint match not found.");
            if (!match.PvpMatchPlayers.Any(x => x.UserId == userId))
                throw new ForbiddenException("You are not a participant in this sprint match.");
            if (match.StatusCode is not ("countdown" or "running"))
                throw new ConflictException("Step session is not available for this match state.");
        }

        var active = await _context.PvpStepSessions
            .Where(x => x.UserId == userId && x.StatusCode == "active")
            .ToListAsync(cancellationToken);
        var captureMode = ResolveStoredCaptureMode(request);
        var activeSessionIds = active.Select(x => x.StepSessionId).ToArray();
        var legacySimpleSessionIds = request.ContractVersion >= 3 && captureMode == "dual"
            ? (await _context.ValidatedStepRecords
                .AsNoTracking()
                .Where(x =>
                    x.StepSessionId.HasValue &&
                    activeSessionIds.Contains(x.StepSessionId.Value) &&
                    x.SourceCode == SimpleTemporalPolicyBConstants.ValidationMode)
                .Select(x => x.StepSessionId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : [];
        var reusable = active.FirstOrDefault(x =>
            x.PurposeCode == purposeCode &&
            x.MatchId == matchId &&
            x.ExpiresAt > now &&
            x.ContractVersion == request.ContractVersion &&
            x.SensorModeCode == captureMode &&
            !legacySimpleSessionIds.Contains(x.StepSessionId));
        if (reusable != null)
        {
            return await ToSessionResponseAsync(reusable, now, cancellationToken);
        }
        foreach (var session in active)
        {
            session.StatusCode = session.ExpiresAt <= now ? "expired" : "closed";
            session.ClosedReason = purposeCode == "pvp" ? "pvp_session_started" : "replaced";
        }

        var expiresAt = purposeCode == "daily"
            ? NextVietnamDayExpiryUtc(now)
            : ResolvePvpSessionExpiry(match!, now);
        var created = new PvpStepSession
        {
            StepSessionId = Guid.NewGuid(),
            MatchId = matchId,
            UserId = userId,
            PurposeCode = purposeCode,
            PlatformCode = request.PlatformCode,
            SensorModeCode = captureMode,
            ContractVersion = request.ContractVersion,
            CaptureMetadataJson = request.CaptureMetadata?.GetRawText(),
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            StatusCode = "active",
            ExpiresAt = expiresAt,
            CreatedAt = now
        };
        _context.PvpStepSessions.Add(created);
        await _context.SaveChangesAsync(cancellationToken);
        return await ToSessionResponseAsync(created, now, cancellationToken);
        });
    }

    private async Task<PvpStepBatchResponse> SubmitV3BatchAsync(
        Guid userId,
        Guid? matchId,
        Guid sessionId,
        string purposeCode,
        SubmitPvpStepBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Events.Count != 0)
            throw new BadRequestException("Contract v3 does not accept synthetic or legacy step events.");
        if (request.DetectorEvents.Count > _options.MaxBatchEvents)
            throw new BadRequestException(
                $"A v3 batch cannot contain more than {_options.MaxBatchEvents} detector events.");
        if (request.CounterSamples.Count > _options.MaxBatchCounterSamples)
            throw new BadRequestException(
                $"A v3 batch cannot contain more than {_options.MaxBatchCounterSamples} counter samples.");
        if (request.MotionWindows.Count > _options.MaxBatchMotionWindows)
            throw new BadRequestException(
                $"A step batch cannot contain more than {_options.MaxBatchMotionWindows} motion windows.");

        return await _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
        {
            var now = DateTime.UtcNow;
            var session = await _context.PvpStepSessions
                .FirstOrDefaultAsync(x =>
                    x.StepSessionId == sessionId &&
                    x.UserId == userId &&
                    x.PurposeCode == purposeCode &&
                    x.MatchId == matchId,
                    cancellationToken)
                ?? throw new NotFoundException("Step sensor session not found.");
            if (session.ContractVersion != 3)
                throw new ConflictException("This step session was not created for contract v3.");
            if (session.StatusCode != "active" || session.ExpiresAt < now)
                throw new ConflictException("Step sensor session is not active.");
            if (!string.Equals(request.Nonce, session.Nonce, StringComparison.Ordinal))
                throw new BadRequestException("Step session nonce is invalid.");

            var captureMode = ToApiCaptureMode(session.SensorModeCode);
            var expectedHash = StepSensorCanonicalizer.ComputeV3Hash(
                sessionId,
                request.Sequence,
                request.Nonce,
                captureMode,
                request.DetectorEvents,
                request.CounterSamples,
                request.MotionWindows);
            if (!string.Equals(expectedHash, request.PayloadHash, StringComparison.Ordinal))
                throw new BadRequestException(
                    "Payload hash verification failed; uppercase SHA-256 is required.");

            var existing = await _context.StepSensorBatches.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.StepSessionId == sessionId && x.Sequence == request.Sequence,
                    cancellationToken);
            if (existing != null)
            {
                if (!string.Equals(existing.PayloadHash, request.PayloadHash, StringComparison.Ordinal))
                    throw new ConflictException(
                        "This sequence was already submitted with a different payload.");
                return await BuildV3ResponseAsync(
                    session, existing, matchId, userId, null, cancellationToken);
            }
            if (request.Sequence != session.LastSequence + 1)
                throw new ConflictException($"Expected sequence {session.LastSequence + 1}.");
            if (request.DetectorEvents.Count == 0 && request.CounterSamples.Count == 0)
            {
                var hasPendingRecord = await _context.ValidatedStepRecords.AnyAsync(
                    x => x.StepSessionId == sessionId && x.ValidationStatus == "pending",
                    cancellationToken);
                var hasPendingSegment = await _context.StepSensorBatches.AnyAsync(
                    x =>
                        x.StepSessionId == sessionId &&
                        x.ReconciliationStatus == "pending_reconciliation" &&
                        x.ReconciliationReason == SimpleTemporalSegmentConstants.OpenReasonCode,
                    cancellationToken);
                if (!hasPendingRecord && !hasPendingSegment)
                    throw new BadRequestException(
                        "An empty v3 batch is allowed only as a reconciliation heartbeat.");
            }

            ValidateV3IdentityShape(request);
            await RejectV3ReplayAsync(sessionId, request, cancellationToken);
            await ValidateV3MonotonicIdentityAsync(sessionId, request, cancellationToken);

            PvpMatch? match = null;
            PvpMatchPlayer? player = null;
            List<PvpMatchEffect> effects = [];
            if (purposeCode == "pvp")
            {
                match = await _context.PvpMatches.Include(x => x.PvpMatchPlayers)
                    .FirstOrDefaultAsync(x => x.MatchId == matchId, cancellationToken)
                    ?? throw new NotFoundException("Sprint match not found.");
                if (match.StatusCode is not ("running" or "settling"))
                    throw new ConflictException(
                        "Sprint step batches are accepted only while running or settling.");
                player = match.PvpMatchPlayers.SingleOrDefault(x => x.UserId == userId)
                    ?? throw new ForbiddenException(
                        "You are not a participant in this sprint match.");
                effects = await _context.PvpMatchEffects.AsNoTracking()
                    .Where(x => x.MatchId == matchId &&
                                x.TargetMatchPlayerId == player.MatchPlayerId &&
                                (x.EffectKindCode == "buff" || x.EffectKindCode == "debuff"))
                    .ToListAsync(cancellationToken);
            }

            var attestation = await VerifyBatchAttestationAsync(
                session, request, now, cancellationToken);
            var previousMotionWindows = await LoadHistoricalV3MotionWindowsAsync(
                sessionId,
                request.DetectorEvents,
                cancellationToken);
            var motion = MotionValidationEngine.EvaluateV3(
                request.DetectorEvents,
                request.MotionWindows,
                previousMotionWindows,
                _motionOptions);
            LogV3MotionCoverage(sessionId, request.DetectorEvents, motion);

            var batch = new StepSensorBatch
            {
                StepSensorBatchId = Guid.NewGuid(),
                StepSessionId = sessionId,
                Sequence = request.Sequence,
                PayloadHash = request.PayloadHash,
                AttestationStatus = attestation.Status,
                PackageName = attestation.PackageName,
                VerdictTimestamp = attestation.VerdictTimestamp,
                VerdictJson = attestation.VerdictJson,
                EvidenceVersion = 3,
                MotionScore = motion.Score,
                MotionStatus = motion.Status,
                MotionReasonsJson = JsonSerializer.Serialize(motion.Reasons),
                DegradedEvidence = motion.DegradedEvidence,
                ReconciliationStatus = "pending_reconciliation",
                ReconciliationReason = "counter_reconciliation_pending",
                ReceivedAt = now
            };
            _context.StepSensorBatches.Add(batch);
            PersistV3MotionWindows(batch.StepSensorBatchId, request, motion);

            for (var index = 0; index < request.CounterSamples.Count; index++)
            {
                var item = request.CounterSamples[index];
                _context.StepCounterEvidenceSamples.Add(new StepCounterEvidenceSample
                {
                    CounterSampleId = Guid.NewGuid(),
                    BatchId = batch.StepSensorBatchId,
                    SampleIndex = checked((short)index),
                    ClientSampleId = item.ClientSampleId,
                    BootSessionId = item.BootSessionId,
                    SensorElapsedRealtimeNs = item.SensorElapsedRealtimeNs,
                    ObservedAt = AsUtc(item.ObservedAt),
                    CounterTotal = item.CounterTotal
                });
            }

            var newDetectorRecords = new List<ValidatedStepRecord>();
            for (var index = 0; index < request.DetectorEvents.Count; index++)
            {
                var item = request.DetectorEvents[index];
                var recordedAt = AsUtc(item.RecordedAt);
                var status = "pending";
                string? reason = "counter_reconciliation_pending";
                if (recordedAt > now.AddSeconds(_options.FutureToleranceSeconds))
                {
                    status = "suspicious";
                    reason = "timestamp_in_future";
                }
                else if (recordedAt < now.AddSeconds(-_options.MaxEvidenceAgeSeconds))
                {
                    status = "rejected";
                    reason = "expired_unverified";
                }
                else if (purposeCode == "pvp" &&
                         !StepSensorRules.IsIntervalWithinRace(
                             recordedAt, recordedAt, match!.StartedAt, match.EndedAt))
                {
                    status = "rejected";
                    reason = "outside_sprint_window";
                }
                else if (!attestation.IsValid)
                {
                    status = "suspicious";
                    reason = attestation.RejectionReason ?? "attestation_failed";
                }
                else if (session.SensorModeCode == "detector")
                {
                    status = "pending";
                    reason = "detector_only_validation_pending";
                }

                var motionEvent = motion.Events.TryGetValue(index, out var evaluatedMotion)
                    ? evaluatedMotion
                    : new MotionEventEvaluation(
                        0, "unavailable", true, ["motion_evidence_missing"]);
                var recordHash = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        $"{request.PayloadHash}:detector:{item.ClientEventId:D}")));
                var record = new ValidatedStepRecord
                {
                    ValidatedStepRecordId = Guid.NewGuid(),
                    UserId = userId,
                    StepSessionId = sessionId,
                    BatchId = batch.StepSensorBatchId,
                    EventIndex = index,
                    ClientEventId = item.ClientEventId,
                    BootSessionId = item.BootSessionId,
                    SensorElapsedRealtimeNs = item.SensorElapsedRealtimeNs,
                    PlatformCode = session.PlatformCode,
                    SourceCode = "step_detector",
                    SensorModeCode = session.SensorModeCode,
                    IntervalStartedAt = recordedAt,
                    RecordedAt = recordedAt,
                    StepCount = 1,
                    EligibleStepCount = 0,
                    SequenceNumber = request.Sequence,
                    PayloadHash = recordHash,
                    ValidationStatus = status,
                    RejectionReason = reason,
                    MotionScore = motionEvent.Score,
                    MotionStatus = StepMotionEvidenceRules.NormalizeStatus(
                        motionEvent.Status,
                        motionEvent.Reasons),
                    ReceivedAt = now
                };
                _context.ValidatedStepRecords.Add(record);
                newDetectorRecords.Add(record);
            }

            session.LastSequence = request.Sequence;
            session.LastSubmittedAt = now;
            await _context.SaveChangesAsync(cancellationToken);

            if (request.MotionWindows.Count > 0)
            {
                await ReevaluatePendingV3MotionAsync(
                    sessionId,
                    request.MotionWindows,
                    cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var previousDailyValidatedSteps = purposeCode == "daily"
                ? await GetTotalDailyValidatedStepsAsync(userId, cancellationToken)
                : 0L;
            var resolutionRecords = new List<ValidatedStepRecord>();
            var toAccept = new List<ValidatedStepRecord>();
            IReadOnlyList<CounterRecoveryShadowInterval> counterRecoveryShadowIntervals = [];

            if (session.SensorModeCode == "detector")
            {
                toAccept.AddRange(newDetectorRecords.Where(x => x.ValidationStatus == "pending"));
                resolutionRecords.AddRange(newDetectorRecords);
            }
            else if (session.SensorModeCode == "counter")
            {
                var counterRecords = await CreateCounterOnlyRecordsAsync(
                    session, batch, request, attestation, match, now, cancellationToken);
                toAccept.AddRange(counterRecords);
            }
            else
            {
                var reconciliation = await ReconcileDualSourceAsync(
                    session, batch, now, cancellationToken);
                toAccept.AddRange(reconciliation.SupportedCandidates);
                resolutionRecords.AddRange(reconciliation.Resolutions);
                counterRecoveryShadowIntervals = reconciliation.ShadowIntervals;
            }

            var newlyAuthoritative = await FinalizeV3AcceptedRecordsAsync(
                userId, purposeCode, toAccept, now, cancellationToken);
            resolutionRecords.AddRange(toAccept);
            await _context.SaveChangesAsync(cancellationToken);

            if (counterRecoveryShadowIntervals.Count > 0)
            {
                await EmitCounterRecoveryShadowAssessmentsAsync(
                    session,
                    batch.StepSensorBatchId,
                    counterRecoveryShadowIntervals,
                    cancellationToken);
            }

            var newlySimpleAuthoritative = new List<ValidatedStepRecord>();
            if (_options.SimpleStepValidationEnabled)
            {
                if (string.Equals(
                        _options.SimpleStepValidationRevision,
                        SimpleTemporalPolicyBConstants.Revision,
                        StringComparison.Ordinal))
                {
                    newlySimpleAuthoritative = await FinalizeSimpleTemporalPolicyBAsync(
                        session,
                        batch,
                        attestation.IsValid,
                        match,
                        now,
                        cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unknown Simple step validation revision '{_options.SimpleStepValidationRevision}'.");
                }
            }

            var progressRecords = newlyAuthoritative
                .Concat(newlySimpleAuthoritative)
                .DistinctBy(x => x.ValidatedStepRecordId)
                .ToList();
            var authoritativePipelineEnabled =
                _options.V3AuthoritativeEnabled ||
                _options.SimpleStepValidationAuthoritativeEnabled;

            var lastMultiplier = PvpGameplayCalculator.BaseSpeedBps;
            if (authoritativePipelineEnabled &&
                progressRecords.Count > 0 &&
                player != null &&
                match!.ScoringModeCode == "legacy_race_steps")
            {
                long distanceAdded = 0;
                foreach (var record in progressRecords)
                {
                    lastMultiplier = StepSensorRules.MinimumPvpMultiplier(
                        match, player, record.IntervalStartedAt, record.RecordedAt, effects);
                    distanceAdded = checked(distanceAdded +
                        PvpGameplayCalculator.CalculateDistanceUnits(
                            record.EligibleStepCount, lastMultiplier));
                }
                var accepted = progressRecords.Sum(x => x.EligibleStepCount);
                player.ValidatedSteps = checked(player.ValidatedSteps + accepted);
                player.DistanceUnits = checked(player.DistanceUnits + distanceAdded);
                player.Score = (int)Math.Min(
                    int.MaxValue,
                    player.DistanceUnits / PvpGameplayCalculator.DistanceUnitsPerStep);
                AddMatchProgressEvent(match, player, accepted, lastMultiplier, now);
            }

            var newlyAcceptedSteps = progressRecords.Sum(x => x.EligibleStepCount);
            int? newPetLevel = null;
            if (authoritativePipelineEnabled &&
                purposeCode == "daily" &&
                newlyAcceptedSteps > 0)
            {
                var progressionSettings = await LoadProgressionSettingsAsync(cancellationToken);
                var rewardsCrossed = StepExperienceReward.CalculateRewardsCrossed(
                    previousDailyValidatedSteps, newlyAcceptedSteps);
                if (rewardsCrossed > 0)
                {
                    newPetLevel = await AwardPetExperienceAsync(
                        userId,
                        checked(rewardsCrossed * progressionSettings.ExpPerMilestone),
                        progressionSettings.ExpIncreasePerLevel,
                        now,
                        cancellationToken);
                }
            }

            await RefreshV3BatchStatusesAsync(sessionId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            if (authoritativePipelineEnabled)
            {
                await SyncAcceptedProgressAsync(
                    userId,
                    newlyAcceptedSteps,
                    newPetLevel,
                    _achievementProgressService,
                    _missionProgressService);
            }

            return await BuildV3ResponseAsync(
                session,
                batch,
                matchId,
                userId,
                resolutionRecords,
                cancellationToken,
                lastMultiplier);
        });
    }

    private async Task<PvpStepBatchResponse> SubmitBatchAsync(
        Guid userId,
        Guid? matchId,
        Guid sessionId,
        string purposeCode,
        SubmitPvpStepBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContractVersion >= 3)
            return await SubmitV3BatchAsync(
                userId, matchId, sessionId, purposeCode, request, cancellationToken);

        if (request.Events.Count < 1 || request.Events.Count > _options.MaxBatchEvents)
            throw new BadRequestException(
                $"A step batch must contain between 1 and {_options.MaxBatchEvents} events.");
        if (request.MotionWindows.Count > _options.MaxBatchMotionWindows)
            throw new BadRequestException(
                $"A step batch cannot contain more than {_options.MaxBatchMotionWindows} motion windows.");
        return await _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
        {
        var now = DateTime.UtcNow;
        var session = await _context.PvpStepSessions
            .FirstOrDefaultAsync(x =>
                x.StepSessionId == sessionId &&
                x.UserId == userId &&
                x.PurposeCode == purposeCode &&
                x.MatchId == matchId,
                cancellationToken)
            ?? throw new NotFoundException("Step sensor session not found.");
        if (session.StatusCode != "active" || session.ExpiresAt < now)
            throw new ConflictException("Step sensor session is not active.");
        if (!string.Equals(request.Nonce, session.Nonce, StringComparison.Ordinal))
            throw new BadRequestException("Step session nonce is invalid.");

        var expectedHash = StepSensorCanonicalizer.ComputeHash(
            sessionId, request.Sequence, request.Nonce, session.SensorModeCode,
            request.ContractVersion, request.Events, request.MotionWindows);
        if (!string.Equals(expectedHash, request.PayloadHash, StringComparison.Ordinal))
            throw new BadRequestException("Payload hash verification failed; uppercase SHA-256 is required.");

        var existing = await _context.StepSensorBatches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StepSessionId == sessionId && x.Sequence == request.Sequence, cancellationToken);
        if (existing != null)
        {
            if (!string.Equals(existing.PayloadHash, request.PayloadHash, StringComparison.Ordinal))
                throw new ConflictException("This sequence was already submitted with a different payload.");
            return await BuildIdempotentResponseAsync(session, existing, matchId, userId, cancellationToken);
        }
        if (request.Sequence != session.LastSequence + 1)
            throw new ConflictException($"Expected sequence {session.LastSequence + 1}.");

        var expPerStepMilestone = 0;
        var expIncreasePerLevel = 0;
        var previousDailyValidatedSteps = 0L;
        if (purposeCode == "daily")
        {
            var progressionSettings = await LoadProgressionSettingsAsync(cancellationToken);
            expPerStepMilestone = progressionSettings.ExpPerMilestone;
            expIncreasePerLevel = progressionSettings.ExpIncreasePerLevel;
            previousDailyValidatedSteps = await GetTotalDailyValidatedStepsAsync(
                userId,
                cancellationToken);
        }

        PvpMatch? match = null;
        PvpMatchPlayer? player = null;
        List<PvpMatchEffect> effects = [];
        if (purposeCode == "pvp")
        {
            match = await _context.PvpMatches.Include(x => x.PvpMatchPlayers)
                .FirstOrDefaultAsync(x => x.MatchId == matchId, cancellationToken)
                ?? throw new NotFoundException("Sprint match not found.");
            if (match.StatusCode is not ("running" or "settling"))
                throw new ConflictException("Sprint step batches are accepted only while running or settling.");
            player = match.PvpMatchPlayers.SingleOrDefault(x => x.UserId == userId)
                ?? throw new ForbiddenException("You are not a participant in this sprint match.");
            effects = await _context.PvpMatchEffects.AsNoTracking()
                .Where(x => x.MatchId == matchId && x.TargetMatchPlayerId == player.MatchPlayerId &&
                            (x.EffectKindCode == "buff" || x.EffectKindCode == "debuff"))
                .ToListAsync(cancellationToken);
        }

        var verifiedSessionBatch = await _context.StepSensorBatches.AsNoTracking()
            .Where(x => x.StepSessionId == sessionId &&
                        (x.AttestationStatus == "verified" ||
                         x.AttestationStatus == "development_bypass"))
            .OrderByDescending(x => x.ReceivedAt)
            .Select(x => new
            {
                x.PackageName,
                x.VerdictTimestamp
            })
            .FirstOrDefaultAsync(cancellationToken);

        AppAttestationResult attestation;
        var hasBatchAttestation = !string.IsNullOrWhiteSpace(request.AttestationToken);
        if (!hasBatchAttestation && !_options.RequirePerBatchAttestation && verifiedSessionBatch != null)
        {
            attestation = new(
                true,
                "legacy_session_cached",
                verifiedSessionBatch.PackageName,
                verifiedSessionBatch.VerdictTimestamp,
                null,
                null);
        }
        else
        {
            try
            {
                attestation = await _attestationVerifier.VerifyAsync(
                    new(request.AttestationToken, request.PayloadHash, session.PlatformCode, now),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                attestation = new(
                    false, "verifier_error", null, null, null, "attestation_verifier_unavailable");
            }
            if (attestation.Status == "rate_limited")
                throw new TooManyRequestsException(
                    "Play Integrity is temporarily rate limited. Retry this batch safely.",
                    60);
        }
        var motion = MotionValidationEngine.Evaluate(request, _motionOptions);
        var batch = new StepSensorBatch
        {
            StepSensorBatchId = Guid.NewGuid(),
            StepSessionId = sessionId,
            Sequence = request.Sequence,
            PayloadHash = request.PayloadHash,
            AttestationStatus = attestation.Status,
            PackageName = attestation.PackageName,
            VerdictTimestamp = attestation.VerdictTimestamp,
            VerdictJson = attestation.VerdictJson,
            EvidenceVersion = request.ContractVersion,
            MotionScore = motion.Score,
            MotionStatus = motion.Status,
            MotionReasonsJson = JsonSerializer.Serialize(motion.Reasons),
            DegradedEvidence = motion.DegradedEvidence,
            ReceivedAt = now
        };
        _context.StepSensorBatches.Add(batch);
        for (var index = 0; index < request.MotionWindows.Count; index++)
        {
            var item = request.MotionWindows[index];
            var result = motion.Windows.Count > index
                ? motion.Windows[index]
                : new MotionWindowEvaluation(
                    index, 0, "rejected", true, false, ["motion_window_not_evaluated"]);
            _context.StepMotionEvidenceWindows.Add(new StepMotionEvidenceWindow
            {
                StepMotionEvidenceWindowId = Guid.NewGuid(),
                BatchId = batch.StepSensorBatchId,
                WindowIndex = checked((short)index),
                WindowStartedAt = item.WindowStartedAt,
                WindowEndedAt = item.WindowEndedAt,
                SampleCount = checked((short)Math.Clamp(item.SampleCount, 0, short.MaxValue)),
                AccelerometerSource = item.AccelerometerSource,
                GyroscopeAvailable = item.GyroscopeAvailable,
                ActivityAvailable = item.ActivityAvailable,
                AccelerationRmsMilli = item.AccelerationRmsMilli,
                AccelerationPeakMilli = item.AccelerationPeakMilli,
                JerkRmsMilli = item.JerkRmsMilli,
                GyroscopeRmsMilli = item.GyroscopeRmsMilli,
                GyroscopePeakMilli = item.GyroscopePeakMilli,
                OrientationDeltaMilliDegrees = item.OrientationDeltaMilliDegrees,
                DominantFrequencyMilliHz = item.DominantFrequencyMilliHz,
                PeriodicityBps = item.PeriodicityBps,
                GaitCycleCount = checked((short)Math.Clamp(item.GaitCycleCount, 0, short.MaxValue)),
                ActivityCode = item.ActivityCode,
                ActivityConfidence = checked((byte)Math.Clamp(item.ActivityConfidence, 0, 100)),
                MotionScore = checked((byte)Math.Clamp(result.Score, 0, 100)),
                Classification = result.Status,
                ReasonCodes = JsonSerializer.Serialize(result.Reasons)
            });
        }

        var detectorWindowStart = request.Events.Min(x => AsUtc(x.RecordedAt)).AddSeconds(-5);
        var detectorWindowEnd = request.Events.Max(x => AsUtc(x.RecordedAt));
        var recentDetectorTimes = session.SensorModeCode == "detector"
            ? await _context.ValidatedStepRecords.AsNoTracking()
                .Where(x => x.UserId == userId &&
                            x.SensorModeCode == "detector" &&
                            x.ValidationStatus == "accepted" &&
                            x.RecordedAt >= detectorWindowStart &&
                            x.RecordedAt <= detectorWindowEnd)
                .Select(x => x.RecordedAt)
                .ToListAsync(cancellationToken)
            : [];
        var cadence = session.SensorModeCode == "detector"
            ? StepSensorRules.ApplyDetectorCadence(request.Events, recentDetectorTimes)
            : new Dictionary<int, StepRuleResult>();

        var accepted = 0;
        long distanceAdded = 0;
        var lastMultiplier = PvpGameplayCalculator.BaseSpeedBps;
        long? rollingSensorTotal = session.LastSensorTotal;
        for (var index = 0; index < request.Events.Count; index++)
        {
            var item = request.Events[index];
            var rule = StepSensorRules.ValidateBasic(
                purposeCode, session.SensorModeCode, item, now,
                _options.FutureToleranceSeconds, _options.DailyBatchMaxAgeSeconds);
            if (rule.IsEligible && cadence.TryGetValue(index, out var cadenceFailure))
                rule = cadenceFailure;
            if (rule.IsEligible && session.LastRecordedAt.HasValue &&
                item.RecordedAt <= session.LastRecordedAt.Value)
                rule = new("suspicious", "recorded_at_not_increasing");
            if (rule.IsEligible && session.SensorModeCode == "counter" &&
                !StepSensorRules.ValidateCounterContinuity(rollingSensorTotal, item.SensorStartTotal).IsEligible)
                rule = StepSensorRules.ValidateCounterContinuity(rollingSensorTotal, item.SensorStartTotal);
            if (rule.IsEligible && purposeCode == "pvp" &&
                !StepSensorRules.IsIntervalWithinRace(
                    item.IntervalStartedAt, item.RecordedAt, match!.StartedAt, match.EndedAt))
                rule = new("rejected", "outside_sprint_window");
            if (rule.IsEligible && !attestation.IsValid)
                rule = new("suspicious", attestation.RejectionReason ?? "attestation_failed");
            var motionEvent = motion.Events.TryGetValue(index, out var evaluatedMotion)
                ? evaluatedMotion
                : new MotionEventEvaluation(
                    0, "rejected", true, ["motion_evidence_missing"]);
            if (rule.IsEligible && _motionOptions.Enforce &&
                motionEvent.Status != "accepted")
            {
                var motionReason = string.Join(',', motionEvent.Reasons);
                rule = new(
                    motionEvent.Status,
                    motionReason.Length <= 200 ? motionReason : motionReason[..200]);
            }

            var rawCount = Math.Max(0, item.StepCount);
            var eligible = rule.IsEligible
                ? await AddDailyEligibleStepsAsync(userId, item.RecordedAt, rawCount, now, cancellationToken)
                : 0;
            if (rule.IsEligible && eligible == 0)
                rule = new("rejected", "daily_step_limit_reached");
            else if (rule.IsEligible && eligible < rawCount)
                rule = new("accepted", "daily_step_limit_partially_applied");

            var recordHash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{request.PayloadHash}:{index}")));
            var record = new ValidatedStepRecord
            {
                ValidatedStepRecordId = Guid.NewGuid(),
                UserId = userId,
                StepSessionId = sessionId,
                BatchId = batch.StepSensorBatchId,
                EventIndex = index,
                PlatformCode = session.PlatformCode,
                SourceCode = "native_sensor",
                SensorModeCode = session.SensorModeCode,
                IntervalStartedAt = item.IntervalStartedAt,
                RecordedAt = item.RecordedAt,
                SensorStartTotal = item.SensorStartTotal,
                SensorEndTotal = item.SensorEndTotal,
                StepCount = rawCount,
                EligibleStepCount = eligible,
                SequenceNumber = request.Sequence,
                PayloadHash = recordHash,
                ValidationStatus = rule.Status,
                RejectionReason = rule.Reason,
                MotionScore = motionEvent.Score,
                MotionStatus = motionEvent.Status,
                ReceivedAt = now
            };
            _context.ValidatedStepRecords.Add(record);

            if (rule.Status == "accepted") batch.AcceptedSteps += eligible;
            else if (rule.Status == "suspicious") batch.SuspiciousSteps += rawCount;
            else batch.RejectedSteps += rawCount;

            if (eligible > 0 &&
                player != null &&
                match!.ScoringModeCode == "legacy_race_steps")
            {
                lastMultiplier = session.SensorModeCode == "counter"
                    ? StepSensorRules.MinimumPvpMultiplier(
                        match!, player, item.IntervalStartedAt, item.RecordedAt, effects)
                    : StepSensorRules.MinimumPvpMultiplier(
                        match!, player, item.RecordedAt, item.RecordedAt, effects);
                var distance = PvpGameplayCalculator.CalculateDistanceUnits(eligible, lastMultiplier);
                accepted += eligible;
                distanceAdded = checked(distanceAdded + distance);
            }
            if (rule.IsEligible && item.SensorEndTotal.HasValue)
                rollingSensorTotal = item.SensorEndTotal;
            if (rule.IsEligible)
                session.LastRecordedAt = item.RecordedAt;
        }

        if (player != null &&
            match!.ScoringModeCode == "legacy_race_steps")
        {
            player.ValidatedSteps = checked(player.ValidatedSteps + accepted);
            player.DistanceUnits = checked(player.DistanceUnits + distanceAdded);
            player.Score = (int)Math.Min(
                int.MaxValue,
                player.DistanceUnits / PvpGameplayCalculator.DistanceUnitsPerStep);
            AddMatchProgressEvent(match!, player, accepted, lastMultiplier, now);
        }
        int? newPetLevel = null;
        if (purposeCode == "daily" && batch.AcceptedSteps > 0)
        {
            var rewardsCrossed = StepExperienceReward.CalculateRewardsCrossed(
                previousDailyValidatedSteps,
                batch.AcceptedSteps);
            if (rewardsCrossed > 0)
            {
                var expToAdd = checked(rewardsCrossed * expPerStepMilestone);
                newPetLevel = await AwardPetExperienceAsync(
                    userId,
                    expToAdd,
                    expIncreasePerLevel,
                    now,
                    cancellationToken);
            }
        }

        await SyncAcceptedProgressAsync(
            userId,
            batch.AcceptedSteps,
            newPetLevel,
            _achievementProgressService,
            _missionProgressService);
        session.LastSequence = request.Sequence;
        session.LastSubmittedAt = now;
        session.LastSensorTotal = rollingSensorTotal;
        await _context.SaveChangesAsync(cancellationToken);
        var dailySnapshot = purposeCode == "daily"
            ? await GetDailySnapshotAsync(userId, now, cancellationToken)
            : null;
        return new PvpStepBatchResponse
        {
            BatchId = batch.StepSensorBatchId,
            AttestationStatus = batch.AttestationStatus,
            AcceptedSteps = batch.AcceptedSteps,
            RejectedSteps = batch.RejectedSteps,
            SuspiciousSteps = batch.SuspiciousSteps,
            NextSequence = session.LastSequence + 1,
            DailyStepDate = dailySnapshot?.Date,
            DailyAcceptedTotal = dailySnapshot?.Total,
            CurrentScore = player?.Score ?? 0,
            ValidatedSteps = player?.ValidatedSteps ?? batch.AcceptedSteps,
            DistanceUnits = player?.DistanceUnits ?? 0,
            SpeedMultiplierBps = lastMultiplier,
            MotionStatus = batch.MotionStatus,
            MotionScore = batch.MotionScore,
            DegradedEvidence = batch.DegradedEvidence,
            MotionReasons = motion.Reasons.ToList()
        };
        });
    }

    private static void ValidateV3IdentityShape(SubmitPvpStepBatchRequest request)
    {
        if (request.DetectorEvents.Any(x =>
                x.ClientEventId == Guid.Empty ||
                x.BootSessionId == Guid.Empty ||
                x.SensorElapsedRealtimeNs <= 0))
            throw new BadRequestException(
                "Every detector event requires non-empty client/boot IDs and a positive monotonic timestamp.");
        if (request.CounterSamples.Any(x =>
                x.ClientSampleId == Guid.Empty ||
                x.BootSessionId == Guid.Empty ||
                x.SensorElapsedRealtimeNs <= 0 ||
                x.CounterTotal < 0))
            throw new BadRequestException(
                "Every counter sample requires non-empty client/boot IDs, a positive monotonic timestamp, and a non-negative total.");
        if (request.MotionWindows.Any(x =>
                x.BootSessionId == Guid.Empty ||
                x.WindowStartElapsedRealtimeNs <= 0 ||
                x.WindowEndElapsedRealtimeNs <= x.WindowStartElapsedRealtimeNs))
            throw new BadRequestException(
                "Every v3 motion window requires a non-empty boot ID and a positive half-open monotonic interval.");
        if (request.DetectorEvents.Select(x => x.ClientEventId).Distinct().Count() !=
            request.DetectorEvents.Count)
            throw new BadRequestException("Detector clientEventId values must be unique within a batch.");
        if (request.CounterSamples.Select(x => x.ClientSampleId).Distinct().Count() !=
            request.CounterSamples.Count)
            throw new BadRequestException("Counter clientSampleId values must be unique within a batch.");
        if (request.DetectorEvents
                .GroupBy(x => new { x.BootSessionId, x.SensorElapsedRealtimeNs })
                .Any(x => x.Count() > 1))
            throw new BadRequestException("Detector boot/elapsed identities must be unique.");
        if (request.CounterSamples
                .GroupBy(x => new { x.BootSessionId, x.SensorElapsedRealtimeNs })
                .Any(x => x.Count() > 1))
            throw new BadRequestException("Counter boot/elapsed identities must be unique.");
    }

    private async Task RejectV3ReplayAsync(
        Guid sessionId,
        SubmitPvpStepBatchRequest request,
        CancellationToken cancellationToken)
    {
        var detectorIds = request.DetectorEvents.Select(x => x.ClientEventId).ToArray();
        if (detectorIds.Length > 0 && await _context.ValidatedStepRecords.AsNoTracking().AnyAsync(
                x => x.StepSessionId == sessionId &&
                     x.ClientEventId.HasValue &&
                     detectorIds.Contains(x.ClientEventId.Value),
                cancellationToken))
            throw new ConflictException("A detector clientEventId has already been recorded.");

        var counterIds = request.CounterSamples.Select(x => x.ClientSampleId).ToArray();
        if (counterIds.Length > 0 && await _context.StepCounterEvidenceSamples.AsNoTracking().AnyAsync(
                x => counterIds.Contains(x.ClientSampleId),
                cancellationToken))
            throw new ConflictException("A counter clientSampleId has already been recorded.");
    }

    private async Task ValidateV3MonotonicIdentityAsync(
        Guid sessionId,
        SubmitPvpStepBatchRequest request,
        CancellationToken cancellationToken)
    {
        var detectorBootIds = request.DetectorEvents.Select(x => x.BootSessionId).Distinct().ToArray();
        if (detectorBootIds.Length > 0)
        {
            var prior = await _context.ValidatedStepRecords.AsNoTracking()
                .Where(x => x.StepSessionId == sessionId &&
                            x.BootSessionId.HasValue &&
                            detectorBootIds.Contains(x.BootSessionId.Value) &&
                            x.SensorElapsedRealtimeNs.HasValue)
                .Select(x => new { Boot = x.BootSessionId!.Value, Elapsed = x.SensorElapsedRealtimeNs!.Value })
                .ToListAsync(cancellationToken);
            foreach (var group in request.DetectorEvents.GroupBy(x => x.BootSessionId))
            {
                var previous = prior.Where(x => x.Boot == group.Key)
                    .Select(x => (long?)x.Elapsed).Max();
                if (previous.HasValue && group.Min(x => x.SensorElapsedRealtimeNs) <= previous.Value)
                    throw new ConflictException(
                        "Detector monotonic time must increase within a boot session.");
            }
        }

        var counterBootIds = request.CounterSamples.Select(x => x.BootSessionId).Distinct().ToArray();
        if (counterBootIds.Length > 0)
        {
            var prior = await _context.StepCounterEvidenceSamples.AsNoTracking()
                .Where(x => x.Batch.StepSessionId == sessionId &&
                            counterBootIds.Contains(x.BootSessionId))
                .Select(x => new { x.BootSessionId, x.SensorElapsedRealtimeNs })
                .ToListAsync(cancellationToken);
            foreach (var group in request.CounterSamples.GroupBy(x => x.BootSessionId))
            {
                var previous = prior.Where(x => x.BootSessionId == group.Key)
                    .Select(x => (long?)x.SensorElapsedRealtimeNs).Max();
                if (previous.HasValue && group.Min(x => x.SensorElapsedRealtimeNs) <= previous.Value)
                    throw new ConflictException(
                        "Counter monotonic time must increase within a boot session.");
            }
        }
    }

    private async Task<AppAttestationResult> VerifyBatchAttestationAsync(
        PvpStepSession session,
        SubmitPvpStepBatchRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var verifiedSessionBatch = await _context.StepSensorBatches.AsNoTracking()
            .Where(x => x.StepSessionId == session.StepSessionId &&
                        (x.AttestationStatus == "verified" ||
                         x.AttestationStatus == "development_bypass"))
            .OrderByDescending(x => x.ReceivedAt)
            .Select(x => new { x.PackageName, x.VerdictTimestamp })
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.AttestationToken) &&
            !_options.RequirePerBatchAttestation &&
            verifiedSessionBatch != null)
            return new(
                true,
                "legacy_session_cached",
                verifiedSessionBatch.PackageName,
                verifiedSessionBatch.VerdictTimestamp,
                null,
                null);

        AppAttestationResult result;
        try
        {
            result = await _attestationVerifier.VerifyAsync(
                new(request.AttestationToken, request.PayloadHash, session.PlatformCode, now),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            result = new(
                false, "verifier_error", null, null, null,
                "attestation_verifier_unavailable");
        }
        if (result.Status == "rate_limited")
            throw new TooManyRequestsException(
                "Play Integrity is temporarily rate limited. Retry this batch safely.", 60);
        return result;
    }

    private async Task<List<StepMotionWindowRequest>> LoadHistoricalV3MotionWindowsAsync(
        Guid sessionId,
        IReadOnlyList<StepDetectorEventRequest> detectorEvents,
        CancellationToken cancellationToken)
    {
        var result = new List<StepMotionWindowRequest>();
        foreach (var bootGroup in detectorEvents.GroupBy(x => x.BootSessionId))
        {
            var minimumEventNs = bootGroup.Min(x => x.SensorElapsedRealtimeNs);
            var maximumEventNs = bootGroup.Max(x => x.SensorElapsedRealtimeNs);
            var rangeStartNs = Math.Max(0, minimumEventNs - MotionHistoryRetentionNs);
            var rangeEndNs = maximumEventNs > long.MaxValue - MotionHistoryRetentionNs
                ? long.MaxValue
                : maximumEventNs + MotionHistoryRetentionNs;
            var rows = await _context.StepMotionEvidenceWindows
                .AsNoTracking()
                .Where(x =>
                    x.Batch.StepSessionId == sessionId &&
                    x.BootSessionId == bootGroup.Key &&
                    x.WindowStartElapsedRealtimeNs.HasValue &&
                    x.WindowEndElapsedRealtimeNs.HasValue &&
                    x.WindowEndElapsedRealtimeNs > rangeStartNs &&
                    x.WindowStartElapsedRealtimeNs <= rangeEndNs)
                .OrderBy(x => x.WindowStartElapsedRealtimeNs)
                .ThenBy(x => x.WindowEndElapsedRealtimeNs)
                .Select(x => new StepMotionWindowRequest
                {
                    BootSessionId = x.BootSessionId!.Value,
                    WindowStartElapsedRealtimeNs = x.WindowStartElapsedRealtimeNs!.Value,
                    WindowEndElapsedRealtimeNs = x.WindowEndElapsedRealtimeNs!.Value,
                    WindowStartedAt = x.WindowStartedAt,
                    WindowEndedAt = x.WindowEndedAt,
                    SampleCount = x.SampleCount,
                    AccelerometerSource = x.AccelerometerSource,
                    GyroscopeAvailable = x.GyroscopeAvailable,
                    ActivityAvailable = x.ActivityAvailable,
                    AccelerationRmsMilli = x.AccelerationRmsMilli,
                    AccelerationPeakMilli = x.AccelerationPeakMilli,
                    JerkRmsMilli = x.JerkRmsMilli,
                    GyroscopeRmsMilli = x.GyroscopeRmsMilli,
                    GyroscopePeakMilli = x.GyroscopePeakMilli,
                    OrientationDeltaMilliDegrees = x.OrientationDeltaMilliDegrees,
                    DominantFrequencyMilliHz = x.DominantFrequencyMilliHz,
                    PeriodicityBps = x.PeriodicityBps,
                    GaitCycleCount = x.GaitCycleCount,
                    ActivityCode = x.ActivityCode,
                    ActivityConfidence = x.ActivityConfidence
                })
                .ToListAsync(cancellationToken);
            result.AddRange(rows);
        }

        return result
            .DistinctBy(x => new
            {
                x.BootSessionId,
                x.WindowStartElapsedRealtimeNs,
                x.WindowEndElapsedRealtimeNs
            })
            .ToList();
    }

    private async Task ReevaluatePendingV3MotionAsync(
        Guid sessionId,
        IReadOnlyList<StepMotionWindowRequest> currentWindows,
        CancellationToken cancellationToken)
    {
        var pendingBatchIds = await _context.ValidatedStepRecords
            .Where(x =>
                x.StepSessionId == sessionId &&
                x.SourceCode == "step_detector" &&
                x.ValidationStatus == "pending" &&
                x.MotionStatus == "unavailable" &&
                x.BatchId.HasValue)
            .Select(x => x.BatchId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var batchId in pendingBatchIds)
        {
            var records = await _context.ValidatedStepRecords
                .Include(x => x.Batch)
                .Where(x =>
                    x.BatchId == batchId &&
                    x.SourceCode == "step_detector" &&
                    x.ClientEventId.HasValue &&
                    x.BootSessionId.HasValue &&
                    x.SensorElapsedRealtimeNs.HasValue)
                .OrderBy(x => x.EventIndex)
                .ThenBy(x => x.SensorElapsedRealtimeNs)
                .ToListAsync(cancellationToken);
            if (records.Count == 0 ||
                !records.Any(x =>
                    x.ValidationStatus == "pending" &&
                    x.MotionStatus == "unavailable"))
                continue;

            var detectorEvents = records.Select(x => new StepDetectorEventRequest
            {
                ClientEventId = x.ClientEventId!.Value,
                BootSessionId = x.BootSessionId!.Value,
                SensorElapsedRealtimeNs = x.SensorElapsedRealtimeNs!.Value,
                RecordedAt = x.RecordedAt
            }).ToArray();
            var historicalWindows = await LoadHistoricalV3MotionWindowsAsync(
                sessionId,
                detectorEvents,
                cancellationToken);
            var evaluation = MotionValidationEngine.EvaluateV3(
                detectorEvents,
                currentWindows,
                historicalWindows,
                _motionOptions);
            LogV3MotionCoverage(sessionId, detectorEvents, evaluation);

            var originalBatch = records[0].Batch!;
            originalBatch.MotionScore = evaluation.Score;
            originalBatch.MotionStatus = evaluation.Status;
            originalBatch.MotionReasonsJson = JsonSerializer.Serialize(evaluation.Reasons);
            originalBatch.DegradedEvidence = evaluation.DegradedEvidence;

            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record.ValidationStatus != "pending" ||
                    record.MotionStatus != "unavailable" ||
                    !evaluation.Events.TryGetValue(index, out var motionEvent))
                    continue;

                var normalizedStatus = StepMotionEvidenceRules.NormalizeStatus(
                    motionEvent.Status,
                    motionEvent.Reasons);
                record.MotionScore = motionEvent.Score;
                record.MotionStatus = normalizedStatus;
                if (normalizedStatus != "unavailable" &&
                    record.RejectionReason == StepMotionEvidenceRules.PendingReason)
                {
                    _logger.LogInformation(
                        "V3 pending motion resolved session {SessionId}, event {ClientEventId}, motionStatus={MotionStatus}",
                        sessionId,
                        record.ClientEventId,
                        normalizedStatus);
                    record.RejectionReason = "counter_reconciliation_pending";
                }
            }
        }
    }

    private void LogV3MotionCoverage(
        Guid sessionId,
        IReadOnlyList<StepDetectorEventRequest> detectorEvents,
        MotionBatchEvaluation motion)
    {
        for (var index = 0; index < detectorEvents.Count; index++)
        {
            if (!motion.Events.TryGetValue(index, out var evaluation) ||
                evaluation.Coverage == null)
                continue;
            var coverage = evaluation.Coverage;
            _logger.LogInformation(
                "V3 motion coverage session {SessionId}, event {ClientEventId}: eventElapsedNs={EventElapsedNs}, nearestWindowStart={NearestWindowStart}, nearestWindowEnd={NearestWindowEnd}, gapBeforeNs={GapBeforeNs}, gapAfterNs={GapAfterNs}, matchSource={MatchSource}, gaitStatus={GaitStatus}",
                sessionId,
                detectorEvents[index].ClientEventId,
                coverage.EventElapsedNs,
                coverage.NearestWindowStart,
                coverage.NearestWindowEnd,
                coverage.GapBeforeNs,
                coverage.GapAfterNs,
                coverage.MatchSource,
                evaluation.GaitStatus);
        }
    }

    private void PersistV3MotionWindows(
        Guid batchId,
        SubmitPvpStepBatchRequest request,
        MotionBatchEvaluation motion)
    {
        for (var index = 0; index < request.MotionWindows.Count; index++)
        {
            var item = request.MotionWindows[index];
            var result = motion.Windows.Count > index
                ? motion.Windows[index]
                : new MotionWindowEvaluation(
                    index, 0, "rejected", true, false, ["motion_window_not_evaluated"]);
            _context.StepMotionEvidenceWindows.Add(new StepMotionEvidenceWindow
            {
                StepMotionEvidenceWindowId = Guid.NewGuid(),
                BatchId = batchId,
                WindowIndex = checked((short)index),
                BootSessionId = item.BootSessionId,
                WindowStartElapsedRealtimeNs = item.WindowStartElapsedRealtimeNs,
                WindowEndElapsedRealtimeNs = item.WindowEndElapsedRealtimeNs,
                WindowStartedAt = AsUtc(item.WindowStartedAt),
                WindowEndedAt = AsUtc(item.WindowEndedAt),
                SampleCount = checked((short)Math.Clamp(item.SampleCount, 0, short.MaxValue)),
                AccelerometerSource = item.AccelerometerSource,
                GyroscopeAvailable = item.GyroscopeAvailable,
                ActivityAvailable = item.ActivityAvailable,
                AccelerationRmsMilli = item.AccelerationRmsMilli,
                AccelerationPeakMilli = item.AccelerationPeakMilli,
                JerkRmsMilli = item.JerkRmsMilli,
                GyroscopeRmsMilli = item.GyroscopeRmsMilli,
                GyroscopePeakMilli = item.GyroscopePeakMilli,
                OrientationDeltaMilliDegrees = item.OrientationDeltaMilliDegrees,
                DominantFrequencyMilliHz = item.DominantFrequencyMilliHz,
                PeriodicityBps = item.PeriodicityBps,
                GaitCycleCount = checked((short)Math.Clamp(item.GaitCycleCount, 0, short.MaxValue)),
                ActivityCode = item.ActivityCode,
                ActivityConfidence = checked((byte)Math.Clamp(item.ActivityConfidence, 0, 100)),
                MotionScore = checked((byte)Math.Clamp(result.Score, 0, 100)),
                Classification = result.Status,
                ReasonCodes = JsonSerializer.Serialize(result.Reasons)
            });
        }
    }

    private async Task<List<ValidatedStepRecord>> CreateCounterOnlyRecordsAsync(
        PvpStepSession session,
        StepSensorBatch batch,
        SubmitPvpStepBatchRequest request,
        AppAttestationResult attestation,
        PvpMatch? match,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (request.CounterSamples.Count == 0) return [];
        var bootIds = request.CounterSamples.Select(x => x.BootSessionId).Distinct().ToArray();
        var samples = await _context.StepCounterEvidenceSamples
            .Where(x => x.Batch.StepSessionId == session.StepSessionId &&
                        bootIds.Contains(x.BootSessionId))
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .ToListAsync(cancellationToken);
        var incomingIds = request.CounterSamples.Select(x => x.ClientSampleId).ToHashSet();
        var created = new List<ValidatedStepRecord>();

        foreach (var bootGroup in samples.GroupBy(x => x.BootSessionId))
        {
            var ordered = bootGroup.OrderBy(x => x.SensorElapsedRealtimeNs).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var current = ordered[index];
                if (!incomingIds.Contains(current.ClientSampleId)) continue;
                if (index == 0)
                {
                    _logger.LogInformation(
                        "Walkamon counter baseline created for session {SessionId}, boot {BootSessionId}, reason {Reason}",
                        session.StepSessionId,
                        current.BootSessionId,
                        "first_valid_capture_sample");
                    continue;
                }
                var previous = ordered[index - 1];
                // Walkamon v3 safety policy: first sample, and the first sample after
                // a total reset, establishes a baseline and creates zero steps.
                if (current.CounterTotal < previous.CounterTotal)
                {
                    _logger.LogInformation(
                        "Walkamon counter baseline created for session {SessionId}, boot {BootSessionId}, reason {Reason}",
                        session.StepSessionId,
                        current.BootSessionId,
                        "counter_total_reset");
                    continue;
                }
                if (current.CounterTotal == previous.CounterTotal) continue;
                var delta = current.CounterTotal - previous.CounterTotal;
                var status = "pending";
                string? reason = "counter_delta_pending_authoritative_apply";
                if (delta > int.MaxValue)
                {
                    status = "rejected";
                    reason = "counter_delta_out_of_range";
                }
                else if (current.ObservedAt > now.AddSeconds(_options.FutureToleranceSeconds))
                {
                    status = "suspicious";
                    reason = "timestamp_in_future";
                }
                else if (current.ObservedAt < now.AddSeconds(-_options.MaxEvidenceAgeSeconds))
                {
                    status = "rejected";
                    reason = "expired_unverified";
                }
                else if (session.PurposeCode == "pvp" &&
                         !StepSensorRules.IsIntervalWithinRace(
                             previous.ObservedAt,
                             current.ObservedAt,
                             match!.StartedAt,
                             match.EndedAt))
                {
                    status = "rejected";
                    reason = "outside_sprint_window";
                }
                else if (!attestation.IsValid)
                {
                    status = "suspicious";
                    reason = attestation.RejectionReason ?? "attestation_failed";
                }

                var recordHash = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        $"{batch.PayloadHash}:counter:{current.ClientSampleId:D}")));
                var record = new ValidatedStepRecord
                {
                    ValidatedStepRecordId = Guid.NewGuid(),
                    UserId = session.UserId,
                    StepSessionId = session.StepSessionId,
                    BatchId = batch.StepSensorBatchId,
                    PlatformCode = session.PlatformCode,
                    SourceCode = "step_counter_delta",
                    SensorModeCode = session.SensorModeCode,
                    BootSessionId = current.BootSessionId,
                    SensorElapsedRealtimeNs = current.SensorElapsedRealtimeNs,
                    IntervalStartedAt = previous.ObservedAt,
                    RecordedAt = current.ObservedAt,
                    SensorStartTotal = previous.CounterTotal,
                    SensorEndTotal = current.CounterTotal,
                    StepCount = delta <= int.MaxValue ? (int)delta : 0,
                    EligibleStepCount = 0,
                    SequenceNumber = batch.Sequence,
                    PayloadHash = recordHash,
                    ValidationStatus = status,
                    RejectionReason = reason,
                    MotionScore = batch.MotionScore,
                    MotionStatus = batch.MotionStatus,
                    ReceivedAt = now
                };
                _context.ValidatedStepRecords.Add(record);
                created.Add(record);
            }
        }
        return created.Where(x => x.ValidationStatus == "pending").ToList();
    }

    private async Task<DualReconciliationResult> ReconcileDualSourceAsync(
        PvpStepSession session,
        StepSensorBatch currentBatch,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var detectorRecords = await _context.ValidatedStepRecords
            .Include(x => x.Batch)
            .Where(x => x.StepSessionId == session.StepSessionId &&
                        x.SourceCode == "step_detector" &&
                        x.BootSessionId.HasValue &&
                        x.SensorElapsedRealtimeNs.HasValue)
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .ToListAsync(cancellationToken);
        var samples = await _context.StepCounterEvidenceSamples
            .Include(x => x.Batch)
            .Where(x => x.Batch.StepSessionId == session.StepSessionId)
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .ToListAsync(cancellationToken);
        var pending = detectorRecords.Where(x => x.ValidationStatus == "pending").ToList();
        var handled = new HashSet<Guid>();
        var supportedCandidates = new List<ValidatedStepRecord>();
        var resolutions = new List<ValidatedStepRecord>();
        var shadowIntervals = new List<CounterRecoveryShadowInterval>();
        var settlement = TimeSpan.FromSeconds(Math.Max(1, _options.CounterSettlementSeconds));

        foreach (var bootSamples in samples.GroupBy(x => x.BootSessionId))
        {
            var orderedSamples = bootSamples.OrderBy(x => x.SensorElapsedRealtimeNs).ToArray();
            var segmentStart = 0;
            for (var index = 1; index <= orderedSamples.Length; index++)
            {
                var startsNewSegment = index < orderedSamples.Length &&
                    orderedSamples[index].CounterTotal < orderedSamples[index - 1].CounterTotal;
                if (index < orderedSamples.Length && !startsNewSegment) continue;

                var baseline = orderedSamples[segmentStart];
                var latest = orderedSamples[index - 1];
                var candidates = detectorRecords
                    .Where(x => x.BootSessionId == bootSamples.Key &&
                                x.SensorElapsedRealtimeNs > baseline.SensorElapsedRealtimeNs &&
                                x.SensorElapsedRealtimeNs <= latest.SensorElapsedRealtimeNs)
                    .OrderBy(x => x.SensorElapsedRealtimeNs)
                    .ToList();
                var pendingCandidates = candidates
                    .Where(x => x.ValidationStatus == "pending")
                    .ToList();
                foreach (var record in pendingCandidates)
                    handled.Add(record.ValidatedStepRecordId);

                var detectorCount = candidates.Sum(x => Math.Max(0, x.StepCount));
                var counterDeltaLong = Math.Max(0, latest.CounterTotal - baseline.CounterTotal);
                var counterDelta = counterDeltaLong > int.MaxValue
                    ? int.MaxValue
                    : (int)counterDeltaLong;
                var settlementWatermark = latest.Batch.ReceivedAt;
                if (candidates.Count > 0)
                    settlementWatermark = candidates.Max(x => x.ReceivedAt) > settlementWatermark
                        ? candidates.Max(x => x.ReceivedAt)
                        : settlementWatermark;
                var settled = settlementWatermark.Add(settlement) <= now;
                var decision = StepReconciliationRules.Evaluate(
                    detectorCount, counterDelta, settled);
                latest.Batch.ReconciliationStatus = decision.Status;
                latest.Batch.ReconciliationReason = decision.Reason ??
                    (detectorCount == 0 && counterDelta == 0
                        ? "counter_baseline_or_no_delta"
                        : null);
                _logger.LogInformation(
                    "Step reconciliation session {SessionId}, boot {BootSessionId}: detector={DetectorCount}, counterDelta={CounterDelta}, supportBudget={SupportBudget}, counterExcess={CounterExcess}, settled={Settled}, status={Status}",
                    session.StepSessionId,
                    bootSamples.Key,
                    detectorCount,
                    counterDelta,
                    decision.SupportBudget,
                    decision.CounterExcessSteps,
                    settled,
                    decision.Status);

                if (decision.Status == "pending_reconciliation")
                {
                    segmentStart = index;
                    continue;
                }

                var allocation = StepSupportBudgetRules.Allocate(
                    decision.SupportBudget,
                    candidates.Select(x => new StepSupportCandidate(
                        x.ValidatedStepRecordId,
                        x.ClientEventId ?? Guid.Empty,
                        x.SensorElapsedRealtimeNs ?? 0,
                        x.StepCount,
                        x.ValidationStatus,
                        x.MotionStatus,
                        BatchHasMotionReason(x.Batch, "hard_shake_majority"),
                        StepMotionEvidenceRules.IsLifecycleClosed(
                            x.RecordedAt,
                            now,
                            _options.MaxEvidenceAgeSeconds))));
                var candidatesById = candidates.ToDictionary(x => x.ValidatedStepRecordId);
                foreach (var resolution in allocation.FinalResolutions)
                {
                    var record = candidatesById[resolution.RecordId];
                    if (record.ValidationStatus != "pending") continue;
                    var pendingMotionCreated =
                        resolution.Status == "pending" &&
                        resolution.Reason == StepMotionEvidenceRules.PendingReason &&
                        record.RejectionReason != StepMotionEvidenceRules.PendingReason;
                    record.ValidationStatus = resolution.Status;
                    record.RejectionReason = resolution.Reason;
                    if (pendingMotionCreated)
                    {
                        _logger.LogInformation(
                            "V3 pending motion created session {SessionId}, event {ClientEventId}",
                            session.StepSessionId,
                            record.ClientEventId);
                    }
                    resolutions.Add(record);
                }
                foreach (var recordId in allocation.CandidatesToAccept)
                {
                    var record = candidatesById[recordId];
                    if (record.ValidationStatus == "pending")
                        supportedCandidates.Add(record);
                }

                var existingAggregate = await _context.ValidatedStepRecords
                    .Where(x => x.StepSessionId == session.StepSessionId &&
                                x.SourceCode == "step_counter_delta" &&
                                x.SensorModeCode == "dual" &&
                                x.BootSessionId == bootSamples.Key &&
                                x.SensorElapsedRealtimeNs > baseline.SensorElapsedRealtimeNs &&
                                x.SensorElapsedRealtimeNs <= latest.SensorElapsedRealtimeNs)
                    .SumAsync(x => (int?)x.StepCount, cancellationToken) ?? 0;
                var excess = StepSupportBudgetRules.CalculateIncrementalCounterExcess(
                    detectorCount,
                    counterDelta,
                    existingAggregate);
                if (excess > 0)
                {
                    var aggregateHash = Convert.ToHexString(SHA256.HashData(
                        Encoding.UTF8.GetBytes(
                            $"{session.StepSessionId:D}:counter-excess:{bootSamples.Key:D}:{baseline.SensorElapsedRealtimeNs}:{latest.SensorElapsedRealtimeNs}")));
                    var aggregate = new ValidatedStepRecord
                    {
                        ValidatedStepRecordId = Guid.NewGuid(),
                        UserId = session.UserId,
                        StepSessionId = session.StepSessionId,
                        BatchId = currentBatch.StepSensorBatchId,
                        PlatformCode = session.PlatformCode,
                        SourceCode = "step_counter_delta",
                        SensorModeCode = session.SensorModeCode,
                        BootSessionId = bootSamples.Key,
                        SensorElapsedRealtimeNs = latest.SensorElapsedRealtimeNs,
                        IntervalStartedAt = baseline.ObservedAt,
                        RecordedAt = latest.ObservedAt,
                        SensorStartTotal = baseline.CounterTotal,
                        SensorEndTotal = latest.CounterTotal,
                        StepCount = excess,
                        EligibleStepCount = 0,
                        SequenceNumber = currentBatch.Sequence,
                        PayloadHash = aggregateHash,
                        ValidationStatus = "suspicious",
                        RejectionReason = "counter_excess_without_supported_detector",
                        MotionScore = 0,
                        MotionStatus = "unavailable",
                        ReceivedAt = now
                    };
                    _context.ValidatedStepRecords.Add(aggregate);
                }
                if (decision.CounterExcessSteps > 0)
                {
                    shadowIntervals.Add(new(
                        bootSamples.Key,
                        baseline.SensorElapsedRealtimeNs,
                        latest.SensorElapsedRealtimeNs,
                        baseline.CounterTotal,
                        latest.CounterTotal,
                        counterDelta,
                        detectorCount,
                        decision.SupportBudget));
                }
                segmentStart = index;
            }
        }

        foreach (var record in pending.Where(x => !handled.Contains(x.ValidatedStepRecordId)))
        {
            if (record.ReceivedAt.Add(settlement) > now) continue;
            record.ValidationStatus = "suspicious";
            record.RejectionReason = "counter_evidence_unavailable_after_settlement";
            resolutions.Add(record);
        }

        resolutions.AddRange(detectorRecords.Where(x => x.BatchId == currentBatch.StepSensorBatchId));
        return new(
            supportedCandidates.DistinctBy(x => x.ValidatedStepRecordId).ToList(),
            resolutions.DistinctBy(x => x.ValidatedStepRecordId).ToList(),
            shadowIntervals);
    }

    private async Task EmitCounterRecoveryShadowAssessmentsAsync(
        PvpStepSession session,
        Guid currentBatchId,
        IReadOnlyList<CounterRecoveryShadowInterval> intervals,
        CancellationToken cancellationToken)
    {
        foreach (var interval in intervals)
        {
            var detectorRecords = await _context.ValidatedStepRecords
                .AsNoTracking()
                .Include(x => x.Batch)
                .Where(x =>
                    x.StepSessionId == session.StepSessionId &&
                    x.SourceCode == "step_detector" &&
                    x.BootSessionId == interval.BootSessionId &&
                    x.SensorElapsedRealtimeNs.HasValue &&
                    x.SensorElapsedRealtimeNs > interval.IntervalStartElapsedNs &&
                    x.SensorElapsedRealtimeNs <= interval.IntervalEndElapsedNs)
                .OrderBy(x => x.SensorElapsedRealtimeNs)
                .ThenBy(x => x.ClientEventId)
                .ToListAsync(cancellationToken);
            var motionWindows = await _context.StepMotionEvidenceWindows
                .AsNoTracking()
                .Include(x => x.Batch)
                .Where(x =>
                    x.Batch.StepSessionId == session.StepSessionId &&
                    x.BootSessionId == interval.BootSessionId &&
                    x.WindowStartElapsedRealtimeNs.HasValue &&
                    x.WindowEndElapsedRealtimeNs.HasValue &&
                    x.WindowEndElapsedRealtimeNs > interval.IntervalStartElapsedNs &&
                    x.WindowStartElapsedRealtimeNs <= interval.IntervalEndElapsedNs)
                .OrderBy(x => x.WindowStartElapsedRealtimeNs)
                .ThenBy(x => x.WindowEndElapsedRealtimeNs)
                .ThenBy(x => x.Batch.Sequence)
                .ThenBy(x => x.WindowIndex)
                .ToListAsync(cancellationToken);

            var input = new CounterRecoveryShadowInput(
                session.StepSessionId,
                interval.BootSessionId,
                interval.IntervalStartElapsedNs,
                interval.IntervalEndElapsedNs,
                interval.CounterFrom,
                interval.CounterTo,
                interval.CounterDelta,
                interval.DetectorCount,
                interval.SupportedDetectorCount,
                true,
                detectorRecords.Select(x => new CounterRecoveryShadowDetectorEvidence(
                    x.ClientEventId ?? Guid.Empty,
                    x.BootSessionId ?? Guid.Empty,
                    x.SensorElapsedRealtimeNs ?? 0,
                    x.RecordedAt,
                    x.StepCount,
                    x.ValidationStatus,
                    x.MotionStatus,
                    ParseReasonCodes(x.Batch?.MotionReasonsJson))).ToArray(),
                motionWindows.Select(x => new CounterRecoveryShadowMotionEvidence(
                    x.StepMotionEvidenceWindowId,
                    x.BatchId,
                    x.Batch.Sequence,
                    x.BatchId == currentBatchId,
                    ToV3MotionWindowRequest(x),
                    x.Classification,
                    ParseReasonCodes(x.ReasonCodes),
                    ParseReasonCodes(x.Batch.MotionReasonsJson))).ToArray(),
                _motionOptions);
            var assessment = CounterRecoveryShadowEvaluator.Evaluate(input);
            if (assessment == null) continue;

            _logger.LogInformation(
                "STEP_COUNTER_RECOVERY_SHADOW sessionId={SessionId}, bootSessionId={BootSessionId}, intervalStartElapsedNs={IntervalStartElapsedNs}, intervalEndElapsedNs={IntervalEndElapsedNs}, counterFrom={CounterFrom}, counterTo={CounterTo}, counterDelta={CounterDelta}, detectorCount={DetectorCount}, supportedDetectorCount={SupportedDetectorCount}, detectorAccepted={DetectorAccepted}, detectorSuspicious={DetectorSuspicious}, detectorRejected={DetectorRejected}, detectorPending={DetectorPending}, counterExcess={CounterExcess}, motionWindows={MotionWindows}, motionAccepted={MotionAccepted}, motionSuspicious={MotionSuspicious}, motionRejected={MotionRejected}, motionUnavailable={MotionUnavailable}, hardShakeMajority={HardShakeMajority}, activityDistribution={ActivityDistribution}, gaitDistribution={GaitDistribution}, shadowAssessment={ShadowAssessment}, shadowRecoverableUpperBound={ShadowRecoverableUpperBound}, shadowIntervalId={ShadowIntervalId}, evidenceFingerprint={EvidenceFingerprint}",
                assessment.SessionId,
                assessment.BootSessionId,
                assessment.IntervalStartElapsedNs,
                assessment.IntervalEndElapsedNs,
                assessment.CounterFrom,
                assessment.CounterTo,
                assessment.CounterDelta,
                assessment.DetectorCount,
                assessment.SupportedDetectorCount,
                assessment.DetectorAcceptedCount,
                assessment.DetectorSuspiciousCount,
                assessment.DetectorRejectedCount,
                assessment.DetectorPendingCount,
                assessment.CounterExcess,
                assessment.MotionWindowCount,
                assessment.MotionAcceptedWindowCount,
                assessment.MotionSuspiciousWindowCount,
                assessment.MotionRejectedWindowCount,
                assessment.MotionUnavailableWindowCount,
                assessment.HardShakeMajority,
                JsonSerializer.Serialize(assessment.ActivityDistribution),
                JsonSerializer.Serialize(assessment.GaitDistribution),
                assessment.ShadowAssessment,
                assessment.ShadowRecoverableUpperBound,
                assessment.ShadowIntervalId,
                assessment.EvidenceFingerprint);

            await _benchmarkSink.RecordShadowIntervalAsync(
                session,
                assessment,
                cancellationToken);
        }
    }

    private async Task EmitSimpleStepValidationShadowAsync(
        PvpStepSession session,
        Guid currentBatchId,
        CancellationToken cancellationToken)
    {
        if (session.ContractVersion < 3 || session.SensorModeCode != "dual") return;

        var samples = await _context.StepCounterEvidenceSamples
            .AsNoTracking()
            .Include(x => x.Batch)
            .Where(x => x.Batch.StepSessionId == session.StepSessionId)
            .OrderBy(x => x.BootSessionId)
            .ThenBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ClientSampleId)
            .ToListAsync(cancellationToken);
        if (samples.Count < 2) return;

        var detectorRecords = await _context.ValidatedStepRecords
            .AsNoTracking()
            .Include(x => x.Batch)
            .Where(x =>
                x.StepSessionId == session.StepSessionId &&
                x.SourceCode == "step_detector" &&
                x.BootSessionId.HasValue &&
                x.SensorElapsedRealtimeNs.HasValue)
            .ToListAsync(cancellationToken);
        var allWindows = await _context.StepMotionEvidenceWindows
            .AsNoTracking()
            .Include(x => x.Batch)
            .Where(x =>
                x.Batch.StepSessionId == session.StepSessionId &&
                x.BootSessionId.HasValue &&
                x.WindowStartElapsedRealtimeNs.HasValue &&
                x.WindowEndElapsedRealtimeNs.HasValue)
            .ToListAsync(cancellationToken);
        var currentWindows = allWindows
            .Where(x => x.BatchId == currentBatchId)
            .ToArray();

        foreach (var bootSamples in samples.GroupBy(x => x.BootSessionId))
        {
            var ordered = bootSamples
                .OrderBy(x => x.SensorElapsedRealtimeNs)
                .ThenBy(x => x.ClientSampleId)
                .ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                var interval = SimpleCounterIntervalFactory.Create(
                    new SimpleCounterObservation(
                        previous.ClientSampleId,
                        previous.BootSessionId,
                        previous.SensorElapsedRealtimeNs,
                        previous.CounterTotal),
                    new SimpleCounterObservation(
                        current.ClientSampleId,
                        current.BootSessionId,
                        current.SensorElapsedRealtimeNs,
                        current.CounterTotal));
                if (interval == null || interval.CounterDelta <= 0) continue;

                var hasCurrentTransportEvidence = current.BatchId == currentBatchId ||
                    currentWindows.Any(x =>
                        x.BootSessionId == interval.BootSessionId &&
                        x.WindowEndElapsedRealtimeNs > interval.IntervalStartElapsedNs &&
                        x.WindowStartElapsedRealtimeNs <= interval.IntervalEndElapsedNs);
                if (!hasCurrentTransportEvidence) continue;

                var intervalDetectors = detectorRecords
                    .Where(x =>
                        x.BootSessionId == interval.BootSessionId &&
                        x.SensorElapsedRealtimeNs > interval.IntervalStartElapsedNs &&
                        x.SensorElapsedRealtimeNs <= interval.IntervalEndElapsedNs)
                    .OrderBy(x => x.SensorElapsedRealtimeNs)
                    .ThenBy(x => x.ClientEventId)
                    .ToArray();
                var windows = allWindows
                    .Where(x =>
                        x.BootSessionId == interval.BootSessionId &&
                        x.WindowEndElapsedRealtimeNs > interval.IntervalStartElapsedNs &&
                        x.WindowStartElapsedRealtimeNs <= interval.IntervalEndElapsedNs)
                    .GroupBy(x => new
                    {
                        x.BootSessionId,
                        x.WindowStartElapsedRealtimeNs,
                        x.WindowEndElapsedRealtimeNs
                    })
                    .Select(group => group
                        .OrderByDescending(x => x.BatchId == currentBatchId)
                        .ThenByDescending(x => x.SampleCount)
                        .ThenBy(x => x.Batch.Sequence)
                        .ThenBy(x => x.WindowIndex)
                        .ThenBy(x => x.StepMotionEvidenceWindowId)
                        .First())
                    .OrderBy(x => x.WindowStartElapsedRealtimeNs)
                    .ThenBy(x => x.WindowEndElapsedRealtimeNs)
                    .ThenBy(x => x.StepMotionEvidenceWindowId)
                    .ToArray();

                var motionAccepted = windows.Count(x =>
                    NormalizeSimpleStatus(x.Classification) == "accepted");
                var motionSuspicious = windows.Count(x =>
                    NormalizeSimpleStatus(x.Classification) == "suspicious");
                var motionRejected = windows.Count(x =>
                    NormalizeSimpleStatus(x.Classification) == "rejected");
                var motionUnavailable = windows.Length -
                    motionAccepted - motionSuspicious - motionRejected;
                var hardShakeBatchIds = intervalDetectors
                    .Where(x => BatchHasMotionReason(x.Batch, "hard_shake_majority"))
                    .Select(x => x.BatchId)
                    .Concat(windows
                        .Where(x =>
                            BatchHasMotionReason(x.Batch, "hard_shake_majority") ||
                            ParseReasonCodes(x.ReasonCodes)
                                .Contains("hard_shake_majority", StringComparer.Ordinal))
                        .Select(x => (Guid?)x.BatchId))
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToArray();
                var reasonCodes = windows
                    .SelectMany(x => ParseReasonCodes(x.ReasonCodes)
                        .Concat(ParseReasonCodes(x.Batch.MotionReasonsJson)))
                    .Concat(intervalDetectors.SelectMany(x =>
                        ParseReasonCodes(x.Batch?.MotionReasonsJson)))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var activities = windows
                    .GroupBy(NormalizeSimpleActivityCode)
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(group =>
                    {
                        var confidence = group
                            .Select(x => Math.Clamp((int)x.ActivityConfidence, 0, 100))
                            .ToArray();
                        return new SimpleStepActivityDistribution(
                            group.Key,
                            confidence.Length,
                            confidence.Min(),
                            confidence.Max(),
                            (int)Math.Round(
                                confidence.Average(),
                                MidpointRounding.AwayFromZero));
                    })
                    .ToArray();
                var assessment = SimpleStepIntervalEvaluator.Evaluate(new(
                    session.StepSessionId,
                    interval,
                    intervalDetectors.Sum(x => Math.Max(0, x.StepCount)),
                    windows.Length,
                    motionAccepted,
                    motionSuspicious,
                    motionRejected,
                    motionUnavailable,
                    hardShakeBatchIds.Length,
                    hardShakeBatchIds.Length > 0,
                    activities,
                    reasonCodes,
                    SecurityValid: true,
                    StructureValid: true));

                _logger.LogInformation(
                    "STEP_SIMPLE_VALIDATION_SHADOW sessionId={SessionId}, bootSessionId={BootSessionId}, intervalStartElapsedNs={IntervalStartElapsedNs}, intervalEndElapsedNs={IntervalEndElapsedNs}, counterStart={CounterStart}, counterEnd={CounterEnd}, counterDelta={CounterDelta}, detectorCount={DetectorCount}, motionWindowCount={MotionWindowCount}, motionAccepted={MotionAccepted}, motionSuspicious={MotionSuspicious}, motionRejected={MotionRejected}, motionUnavailable={MotionUnavailable}, hardShakeBatchCount={HardShakeBatchCount}, hardShakeObserved={HardShakeObserved}, activityDistribution={ActivityDistribution}, simpleDecision={SimpleDecision}, shadowSimpleSteps={ShadowSimpleSteps}, reasonCodes={ReasonCodes}, simpleIntervalId={SimpleIntervalId}, evidenceFingerprint={EvidenceFingerprint}, V3AuthoritativeEnabled={V3AuthoritativeEnabled}",
                    assessment.SessionId,
                    assessment.BootSessionId,
                    assessment.IntervalStartElapsedNs,
                    assessment.IntervalEndElapsedNs,
                    assessment.CounterStart,
                    assessment.CounterEnd,
                    assessment.CounterDelta,
                    assessment.DetectorCount,
                    assessment.MotionWindowCount,
                    assessment.MotionAccepted,
                    assessment.MotionSuspicious,
                    assessment.MotionRejected,
                    assessment.MotionUnavailable,
                    assessment.HardShakeBatchCount,
                    assessment.HardShakeObserved,
                    JsonSerializer.Serialize(assessment.ActivityDistribution),
                    assessment.SimpleDecision,
                    assessment.ShadowSimpleSteps,
                    JsonSerializer.Serialize(assessment.ReasonCodes),
                    assessment.SimpleIntervalId,
                    assessment.EvidenceFingerprint,
                    _options.V3AuthoritativeEnabled);

                await _benchmarkSink.RecordSimpleShadowIntervalAsync(
                    session,
                    assessment,
                    _options.V3AuthoritativeEnabled,
                    cancellationToken);
            }
        }
    }

    private async Task<List<ValidatedStepRecord>> FinalizeSimpleTemporalPolicyBAsync(
        PvpStepSession session,
        StepSensorBatch currentBatch,
        bool currentRequestSecurityValid,
        PvpMatch? match,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var newlyAuthoritative = new List<ValidatedStepRecord>();
        if (session.ContractVersion < 3 || session.SensorModeCode != "dual")
            return newlyAuthoritative;

        // A failed trigger request may persist its own evidence for audit, but it
        // must never finalize any interval. A later security-valid request can
        // evaluate the interval and will still inspect the endpoint batches.
        if (!currentRequestSecurityValid)
        {
            _logger.LogWarning(
                "STEP_SIMPLE_AUTHORITATIVE_DECISION skipped sessionId={SessionId}, reason={Reason}",
                session.StepSessionId,
                SimpleTemporalPolicyBReasonCodes.SecurityFailed);
            return newlyAuthoritative;
        }

        var samples = await _context.StepCounterEvidenceSamples
            .Include(x => x.Batch)
            .Where(x => x.Batch.StepSessionId == session.StepSessionId)
            .OrderBy(x => x.BootSessionId)
            .ThenBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ClientSampleId)
            .ToListAsync(cancellationToken);
        if (samples.Count < 2) return newlyAuthoritative;

        var detectorRecords = await _context.ValidatedStepRecords
            .AsNoTracking()
            .Where(x =>
                x.StepSessionId == session.StepSessionId &&
                x.SourceCode == "step_detector" &&
                x.BootSessionId.HasValue &&
                x.SensorElapsedRealtimeNs.HasValue)
            .ToListAsync(cancellationToken);
        var allWindows = await _context.StepMotionEvidenceWindows
            .AsNoTracking()
            .Include(x => x.Batch)
            .Where(x =>
                x.Batch.StepSessionId == session.StepSessionId &&
                x.BootSessionId.HasValue &&
                x.WindowStartElapsedRealtimeNs.HasValue &&
                x.WindowEndElapsedRealtimeNs.HasValue)
            .ToListAsync(cancellationToken);
        var finalizedSegments = await _context.ValidatedStepRecords
            .AsNoTracking()
            .Where(x =>
                x.StepSessionId == session.StepSessionId &&
                x.SourceCode == SimpleTemporalSegmentConstants.RecordSourceCode)
            .OrderBy(x => x.BootSessionId)
            .ThenBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ValidatedStepRecordId)
            .ToListAsync(cancellationToken);
        var existingHashes = finalizedSegments
            .Select(x => x.PayloadHash)
            .ToHashSet(StringComparer.Ordinal);
        var usedEventIndexes = (await _context.ValidatedStepRecords
                .Where(x => x.BatchId == currentBatch.StepSensorBatchId &&
                            x.EventIndex.HasValue)
                .Select(x => x.EventIndex!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var nextAggregateIndex = -1;
        var hasOpenSegment = false;
        var featureFlags = JsonSerializer.Serialize(new
        {
            _options.SimpleStepValidationEnabled,
            _options.SimpleStepValidationAuthoritativeEnabled,
            _options.V3AuthoritativeEnabled
        });

        foreach (var bootSamples in samples.GroupBy(x => x.BootSessionId))
        {
            var ordered = bootSamples
                .OrderBy(x => x.SensorElapsedRealtimeNs)
                .ThenBy(x => x.ClientSampleId)
                .ToArray();
            foreach (var partition in PartitionSimpleCounterRun(ordered))
            {
                if (partition.Length < 2) continue;

                var cursor = 0;
                var partitionFinalized = finalizedSegments
                    .Where(x =>
                        x.BootSessionId == partition[0].BootSessionId &&
                        x.SensorElapsedRealtimeNs >= partition[0].SensorElapsedRealtimeNs &&
                        x.SensorElapsedRealtimeNs <= partition[^1].SensorElapsedRealtimeNs)
                    .OrderBy(x => x.SensorElapsedRealtimeNs)
                    .ThenBy(x => x.ValidatedStepRecordId)
                    .ToArray();
                foreach (var finalized in partitionFinalized)
                {
                    var endpointIndex = FindSimpleSegmentEndpoint(partition, cursor, finalized);
                    if (endpointIndex <= cursor) continue;
                    EmitLateSimpleSegmentEvidenceDiagnostics(
                        session,
                        currentBatch,
                        finalized,
                        partition[cursor],
                        partition[endpointIndex],
                        detectorRecords,
                        allWindows);
                    cursor = endpointIndex;
                }

                if (partition.Length - cursor < 2) continue;
                var openSamples = partition[cursor..];
                var start = openSamples[0];
                var end = openSamples[^1];
                if (end.CounterTotal <= start.CounterTotal) continue;

                var segment = SimpleTemporalSegmentEvaluator.Evaluate(new(
                    session.StepSessionId,
                    openSamples.Select(x => new SimpleTemporalCounterEvidence(
                        x.ClientSampleId,
                        x.BootSessionId,
                        x.SensorElapsedRealtimeNs,
                        x.CounterTotal,
                        x.ObservedAt,
                        x.Batch.ReceivedAt,
                        IsSimpleTrustedAttestation(x.Batch.AttestationStatus),
                        IsSimpleSampleTimeValid(x))).ToArray(),
                    detectorRecords.Select(x => new SimpleTemporalDetectorEvidence(
                        x.ValidatedStepRecordId,
                        x.ClientEventId,
                        x.BootSessionId ?? Guid.Empty,
                        x.SensorElapsedRealtimeNs ?? 0,
                        x.StepCount,
                        x.ValidationStatus,
                        x.ReceivedAt)).ToArray(),
                    allWindows.Select(x => new SimpleTemporalMotionEvidence(
                        new TemporalMotionEvidenceWindow(
                            x.StepMotionEvidenceWindowId,
                            x.BootSessionId ?? Guid.Empty,
                            x.WindowStartElapsedRealtimeNs ?? 0,
                            x.WindowEndElapsedRealtimeNs ?? 0,
                            x.Classification,
                            ParseReasonCodes(x.ReasonCodes),
                            x.ActivityAvailable ? x.ActivityCode : "unavailable",
                            x.ActivityConfidence,
                            x.SampleCount,
                            x.BatchId == currentBatch.StepSensorBatchId),
                        x.Batch.ReceivedAt,
                        x.Batch.Sequence,
                        x.WindowIndex)).ToArray(),
                    now,
                    _options.CounterSettlementSeconds,
                    session.PurposeCode != "pvp" ||
                    match != null &&
                    StepSensorRules.IsIntervalWithinRace(
                        start.ObservedAt,
                        end.ObservedAt,
                        match.StartedAt,
                        match.EndedAt)));

                var temporal = segment.TemporalEvaluation;
                if (segment.Status == SimpleTemporalSegmentStatuses.Open)
                {
                    hasOpenSegment = true;
                    LogSimpleTemporalSegment(
                        session,
                        segment,
                        SimpleTemporalSegmentStatuses.Open,
                        decision: null,
                        reasonCodes: [SimpleTemporalSegmentConstants.OpenReasonCode],
                        authoritativeApplied: false,
                        dailyStepDelta: 0,
                        featureFlags);
                    continue;
                }

                var stableRecordHash = HashSimpleSegment(segment.SegmentId);
                if (existingHashes.Contains(stableRecordHash)) continue;
                var policy = segment.FinalDecision ?? throw new InvalidOperationException(
                    "A FINALIZABLE Simple temporal segment must have a policy decision.");

                while (usedEventIndexes.Contains(nextAggregateIndex))
                    nextAggregateIndex--;
                var aggregateIndex = nextAggregateIndex--;
                usedEventIndexes.Add(aggregateIndex);

                var actualEligible = 0;
                var authoritativeApplied = false;
                var transition = SimpleAuthoritativeTransitionRules.Evaluate(
                    policy,
                    _options.SimpleStepValidationAuthoritativeEnabled,
                    alreadyAuthoritative: false);
                var recordStatus = policy.Decision == SimpleTemporalPolicyDecisions.Allow
                    ? "accepted"
                    : "rejected";
                var reasons = policy.ReasonCodes.ToList();
                if (transition.IsNewAuthoritativeTransition)
                {
                    actualEligible = await AddDailyEligibleStepsAsync(
                        session.UserId,
                        end.ObservedAt,
                        checked((int)transition.NewlyAuthoritativeSteps),
                        now,
                        cancellationToken);
                    authoritativeApplied = actualEligible > 0;
                    if (actualEligible == 0)
                    {
                        recordStatus = "rejected";
                        reasons.Add("daily_step_limit_reached");
                    }
                    else if (actualEligible < policy.EligibleStepCount)
                    {
                        reasons.Add("daily_step_limit_partially_applied");
                    }
                }
                else if (policy.Decision == SimpleTemporalPolicyDecisions.Allow)
                {
                    reasons.Add("simple_authoritative_kill_switch_off");
                }

                var record = new ValidatedStepRecord
                {
                    ValidatedStepRecordId = Guid.NewGuid(),
                    UserId = session.UserId,
                    StepSessionId = session.StepSessionId,
                    BatchId = currentBatch.StepSensorBatchId,
                    EventIndex = aggregateIndex,
                    ClientEventId = null,
                    BootSessionId = segment.BootSessionId,
                    SensorElapsedRealtimeNs = segment.SegmentEndElapsedNs,
                    PlatformCode = session.PlatformCode,
                    SourceCode = SimpleTemporalSegmentConstants.RecordSourceCode,
                    SensorModeCode = session.SensorModeCode,
                    IntervalStartedAt = start.ObservedAt,
                    RecordedAt = end.ObservedAt,
                    SensorStartTotal = segment.CounterStart,
                    SensorEndTotal = segment.CounterEnd,
                    StepCount = segment.AggregateCounterDelta > int.MaxValue
                        ? 0
                        : (int)segment.AggregateCounterDelta,
                    EligibleStepCount = actualEligible,
                    SequenceNumber = currentBatch.Sequence,
                    PayloadHash = stableRecordHash,
                    ValidationStatus = recordStatus,
                    RejectionReason = BuildSimplePolicyAuditReason(reasons),
                    MotionScore = 0,
                    MotionStatus = policy.Decision == SimpleTemporalPolicyDecisions.Allow
                        ? "accepted"
                        : "rejected",
                    ReceivedAt = now
                };
                _context.ValidatedStepRecords.Add(record);
                existingHashes.Add(stableRecordHash);
                foreach (var transportBatch in openSamples
                    .Select(x => x.Batch)
                    .DistinctBy(x => x.StepSensorBatchId)
                    .Where(x =>
                        x.ReconciliationStatus == "pending_reconciliation" &&
                        x.ReconciliationReason == SimpleTemporalSegmentConstants.OpenReasonCode))
                {
                    transportBatch.ReconciliationStatus = recordStatus;
                    transportBatch.ReconciliationReason =
                        "simple_temporal_segment_finalized";
                }
                if (authoritativeApplied)
                    newlyAuthoritative.Add(record);

                LogSimpleTemporalSegment(
                    session,
                    segment,
                    SimpleTemporalSegmentStatuses.Finalized,
                    policy.Decision,
                    reasons,
                    authoritativeApplied,
                    actualEligible,
                    featureFlags);
                _logger.LogInformation(
                    "STEP_SIMPLE_AUTHORITATIVE_DECISION sessionId={SessionId}, bootSessionId={BootSessionId}, simpleSegmentId={SimpleSegmentId}, counterStart={CounterStart}, counterEnd={CounterEnd}, counterDelta={CounterDelta}, intervalCount={IntervalCount}, fraudRegionCount={FraudRegionCount}, maxFraudRegionDurationMs={MaxFraudRegionDurationMs}, policyRevision={PolicyRevision}, decision={Decision}, reasonCodes={ReasonCodes}, eligibleStepCount={EligibleStepCount}, authoritativeEnabled={AuthoritativeEnabled}, previousAuthoritativeState={PreviousAuthoritativeState}, newAuthoritativeState={NewAuthoritativeState}, authoritativeApplied={AuthoritativeApplied}, dailyStepDelta={DailyStepDelta}, featureFlags={FeatureFlags}",
                    session.StepSessionId,
                    segment.BootSessionId,
                    segment.SegmentId,
                    segment.CounterStart,
                    segment.CounterEnd,
                    segment.AggregateCounterDelta,
                    segment.IntervalCount,
                    temporal.FraudRegionCount,
                    temporal.MaxFraudRegionDurationMs,
                    policy.PolicyRevision,
                    policy.Decision,
                    JsonSerializer.Serialize(reasons
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)),
                    policy.EligibleStepCount,
                    _options.SimpleStepValidationAuthoritativeEnabled,
                    false,
                    authoritativeApplied,
                    authoritativeApplied,
                    actualEligible,
                    featureFlags);

                await _benchmarkSink.RecordSimpleTemporalShadowIntervalAsync(
                    session,
                    temporal,
                    cancellationToken);
            }
        }

        if (hasOpenSegment)
        {
            currentBatch.ReconciliationStatus = "pending_reconciliation";
            currentBatch.ReconciliationReason = SimpleTemporalSegmentConstants.OpenReasonCode;
        }

        return newlyAuthoritative;
    }

    internal static IEnumerable<StepCounterEvidenceSample[]> PartitionSimpleCounterRun(
        IReadOnlyList<StepCounterEvidenceSample> ordered)
    {
        var partition = new List<StepCounterEvidenceSample>();
        foreach (var sample in ordered)
        {
            if (partition.Count > 0)
            {
                var previous = partition[^1];
                if (sample.BootSessionId != previous.BootSessionId ||
                    sample.SensorElapsedRealtimeNs <= previous.SensorElapsedRealtimeNs ||
                    sample.CounterTotal < previous.CounterTotal)
                {
                    yield return partition.ToArray();
                    partition.Clear();
                }
            }
            partition.Add(sample);
        }
        if (partition.Count > 0)
            yield return partition.ToArray();
    }

    internal static int FindSimpleSegmentEndpoint(
        IReadOnlyList<StepCounterEvidenceSample> partition,
        int cursor,
        ValidatedStepRecord finalized)
    {
        if (!finalized.SensorElapsedRealtimeNs.HasValue ||
            !finalized.SensorEndTotal.HasValue ||
            cursor < 0 ||
            cursor >= partition.Count ||
            (finalized.SensorStartTotal.HasValue &&
             finalized.SensorStartTotal.Value != partition[cursor].CounterTotal))
            return -1;
        for (var index = Math.Max(1, cursor + 1); index < partition.Count; index++)
        {
            if (partition[index].SensorElapsedRealtimeNs == finalized.SensorElapsedRealtimeNs &&
                partition[index].CounterTotal == finalized.SensorEndTotal)
                return index;
        }
        return -1;
    }

    private void EmitLateSimpleSegmentEvidenceDiagnostics(
        PvpStepSession session,
        StepSensorBatch currentBatch,
        ValidatedStepRecord finalized,
        StepCounterEvidenceSample start,
        StepCounterEvidenceSample end,
        IReadOnlyList<ValidatedStepRecord> detectorRecords,
        IReadOnlyList<StepMotionEvidenceWindow> allWindows)
    {
        var lateWindows = allWindows
            .Where(x =>
                x.BatchId == currentBatch.StepSensorBatchId &&
                x.BootSessionId == end.BootSessionId &&
                x.WindowEndElapsedRealtimeNs > start.SensorElapsedRealtimeNs &&
                x.WindowStartElapsedRealtimeNs < end.SensorElapsedRealtimeNs)
            .ToArray();
        if (lateWindows.Length == 0) return;

        _logger.LogError(
            "STEP_SIMPLE_TEMPORAL_SEGMENT sessionId={SessionId}, bootSessionId={BootSessionId}, status={Status}, decision={Decision}, segmentEndElapsedNs={SegmentEndElapsedNs}, lateMotionWindowCount={LateMotionWindowCount}, reason={Reason}, authoritativeStateUnchanged={AuthoritativeStateUnchanged}",
            session.StepSessionId,
            end.BootSessionId,
            SimpleTemporalSegmentStatuses.Finalized,
            finalized.ValidationStatus,
            end.SensorElapsedRealtimeNs,
            lateWindows.Length,
            SimpleTemporalSegmentConstants.LateEvidenceReasonCode,
            true);

        var lateTemporal = TemporalFraudRegionEvaluator.Evaluate(new(
            session.StepSessionId,
            new SimpleCounterInterval(
                start.ClientSampleId,
                end.ClientSampleId,
                end.BootSessionId,
                start.SensorElapsedRealtimeNs,
                end.SensorElapsedRealtimeNs,
                start.CounterTotal,
                end.CounterTotal,
                Math.Max(0, end.CounterTotal - start.CounterTotal)),
            0,
            lateWindows.Select(x => new TemporalMotionEvidenceWindow(
                x.StepMotionEvidenceWindowId,
                x.BootSessionId ?? Guid.Empty,
                x.WindowStartElapsedRealtimeNs ?? 0,
                x.WindowEndElapsedRealtimeNs ?? 0,
                x.Classification,
                ParseReasonCodes(x.ReasonCodes),
                x.ActivityAvailable ? x.ActivityCode : "unavailable",
                x.ActivityConfidence,
                x.SampleCount,
                true)).ToArray()));
        if (lateTemporal.FraudRegionCount == 0) return;

        var acceptedDetectors = detectorRecords
            .Where(x =>
                x.BootSessionId == end.BootSessionId &&
                x.SensorElapsedRealtimeNs > start.SensorElapsedRealtimeNs &&
                x.SensorElapsedRealtimeNs <= end.SensorElapsedRealtimeNs &&
                x.ValidationStatus == "accepted")
            .OrderBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ClientEventId)
            .ToArray();
        if (acceptedDetectors.Length == 0) return;

        _logger.LogError(
            "STEP_V3_LATE_FRAUD_INCONSISTENCY sessionId={SessionId}, bootSessionId={BootSessionId}, simpleSegmentRecordId={SimpleSegmentRecordId}, acceptedDetectorCount={AcceptedDetectorCount}, fraudRegionCount={FraudRegionCount}, maxFraudRegionDurationMs={MaxFraudRegionDurationMs}, authoritativeStateUnchanged={AuthoritativeStateUnchanged}",
            session.StepSessionId,
            end.BootSessionId,
            finalized.ValidatedStepRecordId,
            acceptedDetectors.Sum(x => Math.Max(0, x.StepCount)),
            lateTemporal.FraudRegionCount,
            lateTemporal.MaxFraudRegionDurationMs,
            true);
    }

    private void LogSimpleTemporalSegment(
        PvpStepSession session,
        SimpleTemporalSegment segment,
        string status,
        string? decision,
        IEnumerable<string> reasonCodes,
        bool authoritativeApplied,
        int dailyStepDelta,
        string featureFlags)
    {
        var temporal = segment.TemporalEvaluation;
        _logger.LogInformation(
            "STEP_SIMPLE_TEMPORAL_SEGMENT sessionId={SessionId}, bootSessionId={BootSessionId}, segmentId={SegmentId}, startSampleId={StartSampleId}, endSampleId={EndSampleId}, startElapsedNs={StartElapsedNs}, endElapsedNs={EndElapsedNs}, counterStart={CounterStart}, counterEnd={CounterEnd}, aggregateCounterDelta={AggregateCounterDelta}, intervalCount={IntervalCount}, detectorCount={DetectorCount}, detectorPendingCount={DetectorPendingCount}, motionWindowCount={MotionWindowCount}, motionAccepted={MotionAccepted}, motionSuspicious={MotionSuspicious}, motionRejected={MotionRejected}, motionUnavailable={MotionUnavailable}, fraudRegionCount={FraudRegionCount}, fraudDurationMs={FraudDurationMs}, maxFraudRegionDurationMs={MaxFraudRegionDurationMs}, evidenceWatermark={EvidenceWatermark}, settlementDeadline={SettlementDeadline}, status={Status}, decision={Decision}, reasonCodes={ReasonCodes}, evidenceFingerprint={EvidenceFingerprint}, authoritativeApplied={AuthoritativeApplied}, dailyStepDelta={DailyStepDelta}, featureFlags={FeatureFlags}",
            session.StepSessionId,
            segment.BootSessionId,
            segment.SegmentId,
            segment.StartClientSampleId,
            segment.EndClientSampleId,
            segment.SegmentStartElapsedNs,
            segment.SegmentEndElapsedNs,
            segment.CounterStart,
            segment.CounterEnd,
            segment.AggregateCounterDelta,
            segment.IntervalCount,
            segment.DetectorCount,
            segment.DetectorPendingCount,
            temporal.MotionWindowCount,
            temporal.MotionAccepted,
            temporal.MotionSuspicious,
            temporal.MotionRejected,
            temporal.MotionUnavailable,
            temporal.FraudRegionCount,
            temporal.FraudDurationMs,
            temporal.MaxFraudRegionDurationMs,
            segment.EvidenceWatermark,
            segment.SettlementDeadline,
            status,
            decision ?? "pending",
            JsonSerializer.Serialize(reasonCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)),
            segment.EvidenceFingerprint,
            authoritativeApplied,
            dailyStepDelta,
            featureFlags);
    }

    private static bool IsSimpleTrustedAttestation(string? status) => status is
        "verified" or
        "development_bypass" or
        "legacy_session_cached" or
        "session_cached";

    private bool IsSimpleSampleTimeValid(StepCounterEvidenceSample sample)
    {
        var observed = AsUtc(sample.ObservedAt);
        var received = AsUtc(sample.Batch.ReceivedAt);
        return observed <= received.AddSeconds(_options.FutureToleranceSeconds) &&
               observed >= received.AddSeconds(-_options.MaxEvidenceAgeSeconds);
    }

    private static string HashSimpleSegment(string simpleSegmentId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{SimpleTemporalSegmentConstants.RecordSourceCode}:" +
            $"{SimpleTemporalPolicyBConstants.Revision}:{simpleSegmentId}")));

    private static string BuildSimplePolicyAuditReason(IEnumerable<string> reasons)
    {
        var value = $"{SimpleTemporalPolicyBConstants.Revision}:" + string.Join(',', reasons
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));
        return value.Length <= 200 ? value : value[..200];
    }

    private static string NormalizeSimpleStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "unavailable"
            : value.Trim().ToLowerInvariant();

    private static string NormalizeSimpleActivityCode(StepMotionEvidenceWindow value) =>
        value.ActivityAvailable && !string.IsNullOrWhiteSpace(value.ActivityCode)
            ? value.ActivityCode.Trim().ToLowerInvariant()
            : "unavailable";

    private async Task EmitSimpleTemporalFraudShadowAsync(
        PvpStepSession session,
        Guid currentBatchId,
        CancellationToken cancellationToken)
    {
        if (session.ContractVersion < 3 || session.SensorModeCode != "dual") return;

        var samples = await _context.StepCounterEvidenceSamples
            .AsNoTracking()
            .Include(x => x.Batch)
            .Where(x => x.Batch.StepSessionId == session.StepSessionId)
            .OrderBy(x => x.BootSessionId)
            .ThenBy(x => x.SensorElapsedRealtimeNs)
            .ThenBy(x => x.ClientSampleId)
            .ToListAsync(cancellationToken);
        if (samples.Count < 2) return;

        var detectorRecords = await _context.ValidatedStepRecords
            .AsNoTracking()
            .Where(x =>
                x.StepSessionId == session.StepSessionId &&
                x.SourceCode == "step_detector" &&
                x.BootSessionId.HasValue &&
                x.SensorElapsedRealtimeNs.HasValue)
            .ToListAsync(cancellationToken);
        var allWindows = await _context.StepMotionEvidenceWindows
            .AsNoTracking()
            .Include(x => x.Batch)
            .Where(x =>
                x.Batch.StepSessionId == session.StepSessionId &&
                x.BootSessionId.HasValue &&
                x.WindowStartElapsedRealtimeNs.HasValue &&
                x.WindowEndElapsedRealtimeNs.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var bootSamples in samples.GroupBy(x => x.BootSessionId))
        {
            var ordered = bootSamples
                .OrderBy(x => x.SensorElapsedRealtimeNs)
                .ThenBy(x => x.ClientSampleId)
                .ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                var interval = SimpleCounterIntervalFactory.Create(
                    new SimpleCounterObservation(
                        previous.ClientSampleId,
                        previous.BootSessionId,
                        previous.SensorElapsedRealtimeNs,
                        previous.CounterTotal),
                    new SimpleCounterObservation(
                        current.ClientSampleId,
                        current.BootSessionId,
                        current.SensorElapsedRealtimeNs,
                        current.CounterTotal));
                if (interval == null || interval.CounterDelta <= 0) continue;

                var overlappingWindows = allWindows
                    .Where(x =>
                        x.BootSessionId == interval.BootSessionId &&
                        x.WindowEndElapsedRealtimeNs > interval.IntervalStartElapsedNs &&
                        x.WindowStartElapsedRealtimeNs < interval.IntervalEndElapsedNs)
                    .ToArray();
                if (current.BatchId != currentBatchId &&
                    !overlappingWindows.Any(x => x.BatchId == currentBatchId))
                    continue;

                var detectorCount = detectorRecords
                    .Where(x =>
                        x.BootSessionId == interval.BootSessionId &&
                        x.SensorElapsedRealtimeNs > interval.IntervalStartElapsedNs &&
                        x.SensorElapsedRealtimeNs <= interval.IntervalEndElapsedNs)
                    .Sum(x => Math.Max(0, x.StepCount));
                var evaluation = TemporalFraudRegionEvaluator.Evaluate(new(
                    session.StepSessionId,
                    interval,
                    detectorCount,
                    overlappingWindows.Select(x => new TemporalMotionEvidenceWindow(
                        x.StepMotionEvidenceWindowId,
                        x.BootSessionId ?? Guid.Empty,
                        x.WindowStartElapsedRealtimeNs ?? 0,
                        x.WindowEndElapsedRealtimeNs ?? 0,
                        x.Classification,
                        ParseReasonCodes(x.ReasonCodes),
                        x.ActivityAvailable ? x.ActivityCode : "unavailable",
                        x.ActivityConfidence,
                        x.SampleCount,
                        x.BatchId == currentBatchId)).ToArray()));

                _logger.LogInformation(
                    "STEP_SIMPLE_TEMPORAL_FRAUD_SHADOW sessionId={SessionId}, bootSessionId={BootSessionId}, counterIntervalId={CounterIntervalId}, intervalStartElapsedNs={IntervalStartElapsedNs}, intervalEndElapsedNs={IntervalEndElapsedNs}, counterDelta={CounterDelta}, fraudRegionCount={FraudRegionCount}, fraudDurationMs={FraudDurationMs}, intervalDurationMs={IntervalDurationMs}, fraudCoverageRatio={FraudCoverageRatio}, hardShakeRegionCount={HardShakeRegionCount}, maxFraudRegionDurationMs={MaxFraudRegionDurationMs}, motionAccepted={MotionAccepted}, motionSuspicious={MotionSuspicious}, motionRejected={MotionRejected}, motionUnavailable={MotionUnavailable}, activityDistribution={ActivityDistribution}, simpleV2EvidenceClass={SimpleV2EvidenceClass}, authoritative={Authoritative}",
                    evaluation.SessionId,
                    evaluation.BootSessionId,
                    evaluation.CounterIntervalId,
                    evaluation.IntervalStartElapsedNs,
                    evaluation.IntervalEndElapsedNs,
                    evaluation.CounterDelta,
                    evaluation.FraudRegionCount,
                    evaluation.FraudDurationMs,
                    evaluation.IntervalDurationMs,
                    evaluation.FraudCoverageRatio,
                    evaluation.HardShakeRegionCount,
                    evaluation.MaxFraudRegionDurationMs,
                    evaluation.MotionAccepted,
                    evaluation.MotionSuspicious,
                    evaluation.MotionRejected,
                    evaluation.MotionUnavailable,
                    JsonSerializer.Serialize(evaluation.ActivityDistribution),
                    evaluation.SimpleV2EvidenceClass,
                    false);

                await _benchmarkSink.RecordSimpleTemporalShadowIntervalAsync(
                    session,
                    evaluation,
                    cancellationToken);
            }
        }
    }

    private static StepMotionWindowRequest ToV3MotionWindowRequest(
        StepMotionEvidenceWindow value) => new()
    {
        BootSessionId = value.BootSessionId ?? Guid.Empty,
        WindowStartElapsedRealtimeNs = value.WindowStartElapsedRealtimeNs ?? 0,
        WindowEndElapsedRealtimeNs = value.WindowEndElapsedRealtimeNs ?? 0,
        WindowStartedAt = value.WindowStartedAt,
        WindowEndedAt = value.WindowEndedAt,
        SampleCount = value.SampleCount,
        AccelerometerSource = value.AccelerometerSource,
        GyroscopeAvailable = value.GyroscopeAvailable,
        ActivityAvailable = value.ActivityAvailable,
        AccelerationRmsMilli = value.AccelerationRmsMilli,
        AccelerationPeakMilli = value.AccelerationPeakMilli,
        JerkRmsMilli = value.JerkRmsMilli,
        GyroscopeRmsMilli = value.GyroscopeRmsMilli,
        GyroscopePeakMilli = value.GyroscopePeakMilli,
        OrientationDeltaMilliDegrees = value.OrientationDeltaMilliDegrees,
        DominantFrequencyMilliHz = value.DominantFrequencyMilliHz,
        PeriodicityBps = value.PeriodicityBps,
        GaitCycleCount = value.GaitCycleCount,
        ActivityCode = value.ActivityCode,
        ActivityConfidence = value.ActivityConfidence
    };

    private static string[] ParseReasonCodes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool BatchHasMotionReason(StepSensorBatch? batch, string reason)
    {
        if (batch == null || string.IsNullOrWhiteSpace(batch.MotionReasonsJson)) return false;
        try
        {
            return (JsonSerializer.Deserialize<string[]>(batch.MotionReasonsJson) ?? [])
                .Contains(reason, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<List<ValidatedStepRecord>> FinalizeV3AcceptedRecordsAsync(
        Guid userId,
        string purposeCode,
        IEnumerable<ValidatedStepRecord> candidates,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var authoritative = new List<ValidatedStepRecord>();
        foreach (var record in candidates
                     .DistinctBy(x => x.ValidatedStepRecordId)
                     .Where(x => x.ValidationStatus == "pending"))
        {
            if (record.SensorModeCode == "dual" &&
                (record.SourceCode != "step_detector" || record.MotionStatus != "accepted"))
            {
                record.ValidationStatus = record.MotionStatus == "rejected"
                    ? "rejected"
                    : "suspicious";
                record.RejectionReason = record.MotionStatus == "rejected"
                    ? "motion_rejected_after_reconciliation"
                    : "motion_unavailable_after_settlement";
                continue;
            }
            if (!_options.V3AuthoritativeEnabled)
            {
                record.ValidationStatus = "accepted";
                record.RejectionReason = "v3_shadow_decision_only";
                continue;
            }

            var eligible = await AddDailyEligibleStepsAsync(
                userId, record.RecordedAt, record.StepCount, now, cancellationToken);
            if (eligible <= 0)
            {
                record.ValidationStatus = "rejected";
                record.RejectionReason = "daily_step_limit_reached";
                continue;
            }
            record.ValidationStatus = "accepted";
            record.RejectionReason = eligible < record.StepCount
                ? "daily_step_limit_partially_applied"
                : null;
            record.EligibleStepCount = eligible;
            authoritative.Add(record);
        }
        return authoritative;
    }

    private async Task RefreshV3BatchStatusesAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var batches = await _context.StepSensorBatches
            .Where(x => x.StepSessionId == sessionId && x.EvidenceVersion >= 3)
            .ToListAsync(cancellationToken);
        var records = await _context.ValidatedStepRecords
            .Where(x => x.StepSessionId == sessionId)
            .ToListAsync(cancellationToken);
        var counterBatchIds = (await _context.StepCounterEvidenceSamples
                .Where(x => x.Batch.StepSessionId == sessionId)
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var sessionHasPending = records.Any(x => x.ValidationStatus == "pending");
        var sessionHasPendingMotion = records.Any(x =>
            x.ValidationStatus == "pending" &&
            x.RejectionReason == StepMotionEvidenceRules.PendingReason);
        foreach (var batch in batches)
        {
            var batchRecords = records.Where(x => x.BatchId == batch.StepSensorBatchId).ToArray();
            var simpleRecords = batchRecords
                .Where(x =>
                    x.SourceCode == SimpleTemporalPolicyBConstants.ValidationMode ||
                    x.SourceCode == SimpleTemporalSegmentConstants.RecordSourceCode)
                .ToArray();
            // When a Simple interval is resolved in this transport batch, its
            // aggregate is the only count accounting source. Detector records
            // remain individual UI resolutions/diagnostics and must not be
            // added to the Counter aggregate.
            var accountingRecords = simpleRecords.Length > 0
                ? simpleRecords
                : batchRecords;
            batch.AcceptedSteps = accountingRecords
                .Where(x => x.ValidationStatus == "accepted")
                .Sum(x => x.EligibleStepCount);
            batch.RejectedSteps = accountingRecords
                .Where(x => x.ValidationStatus == "rejected")
                .Sum(x => x.StepCount);
            batch.SuspiciousSteps = accountingRecords
                .Where(x => x.ValidationStatus == "suspicious")
                .Sum(x => x.StepCount);
            var pendingSteps = batchRecords
                .Where(x => x.ValidationStatus == "pending")
                .Sum(x => x.StepCount);
            if (batch.ReconciliationStatus == "pending_reconciliation" &&
                batch.ReconciliationReason == SimpleTemporalSegmentConstants.OpenReasonCode)
            {
                continue;
            }
            if (batchRecords.Length == 0 &&
                counterBatchIds.Contains(batch.StepSensorBatchId) &&
                batch.ReconciliationStatus != "unavailable" &&
                !string.IsNullOrWhiteSpace(batch.ReconciliationReason))
            {
                continue;
            }
            if (pendingSteps > 0 || (batchRecords.Length == 0 && sessionHasPending))
            {
                batch.ReconciliationStatus = "pending_reconciliation";
                batch.ReconciliationReason = batchRecords.Any(x =>
                    x.ValidationStatus == "pending" &&
                    x.RejectionReason == StepMotionEvidenceRules.PendingReason) ||
                    (batchRecords.Length == 0 && sessionHasPendingMotion)
                    ? StepMotionEvidenceRules.PendingReason
                    : "counter_reconciliation_pending";
            }
            else if (batch.SuspiciousSteps > 0)
            {
                batch.ReconciliationStatus = "suspicious";
                batch.ReconciliationReason = "counter_reconciliation_mismatch_settled";
            }
            else if (batch.RejectedSteps > 0 && batch.AcceptedSteps == 0)
            {
                batch.ReconciliationStatus = "rejected";
                batch.ReconciliationReason = batchRecords.FirstOrDefault(
                    x => x.ValidationStatus == "rejected")?.RejectionReason;
            }
            else
            {
                batch.ReconciliationStatus = "accepted";
                batch.ReconciliationReason = batchRecords.Length == 0
                    ? "counter_baseline_or_no_delta"
                    : null;
            }
        }
    }

    private async Task<PvpStepBatchResponse> BuildV3ResponseAsync(
        PvpStepSession session,
        StepSensorBatch batch,
        Guid? matchId,
        Guid userId,
        IReadOnlyList<ValidatedStepRecord>? resolutionRecords,
        CancellationToken cancellationToken,
        int speedMultiplierBps = PvpGameplayCalculator.BaseSpeedBps)
    {
        var currentRecords = await _context.ValidatedStepRecords.AsNoTracking()
            .Where(x => x.BatchId == batch.StepSensorBatchId)
            .ToListAsync(cancellationToken);
        var responseRecords = resolutionRecords?.ToList() ??
            await LoadV3ResolutionRecordsForBatchAsync(
                batch,
                currentRecords,
                cancellationToken);
        var resolutions = responseRecords
            .Where(x => x.ClientEventId.HasValue)
            .DistinctBy(x => x.ClientEventId)
            .Select(x => new StepDetectorResolutionResponse
            {
                ClientEventId = x.ClientEventId!.Value,
                Status = x.ValidationStatus,
                AcceptedStepCount = x.EligibleStepCount,
                Reason = x.RejectionReason
            })
            .ToList();
        var player = matchId.HasValue
            ? await _context.PvpMatchPlayers.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.MatchId == matchId && x.UserId == userId,
                    cancellationToken)
            : null;
        var dailySnapshot = session.PurposeCode == "daily"
            ? await GetDailySnapshotAsync(userId, DateTime.UtcNow, cancellationToken)
            : null;
        return new PvpStepBatchResponse
        {
            BatchId = batch.StepSensorBatchId,
            AttestationStatus = batch.AttestationStatus,
            AcceptedSteps = batch.AcceptedSteps,
            PendingSteps = currentRecords
                .Where(x => x.ValidationStatus == "pending")
                .Sum(x => x.StepCount),
            RejectedSteps = batch.RejectedSteps,
            SuspiciousSteps = batch.SuspiciousSteps,
            NextSequence = session.LastSequence + 1,
            DailyStepDate = dailySnapshot?.Date,
            DailyAcceptedTotal = dailySnapshot?.Total,
            CurrentScore = player?.Score ?? 0,
            ValidatedSteps = player?.ValidatedSteps ?? dailySnapshot?.Total ?? batch.AcceptedSteps,
            DistanceUnits = player?.DistanceUnits ?? 0,
            SpeedMultiplierBps = speedMultiplierBps,
            MotionStatus = batch.MotionStatus,
            MotionScore = batch.MotionScore,
            DegradedEvidence = batch.DegradedEvidence,
            MotionReasons = JsonSerializer.Deserialize<List<string>>(batch.MotionReasonsJson) ?? [],
            ReconciliationStatus = batch.ReconciliationStatus,
            ReconciliationReason = batch.ReconciliationReason,
            DetectorResolutions = resolutions
        };
    }

    private async Task<List<ValidatedStepRecord>> LoadV3ResolutionRecordsForBatchAsync(
        StepSensorBatch batch,
        IReadOnlyList<ValidatedStepRecord> currentRecords,
        CancellationToken cancellationToken)
    {
        var windows = await _context.StepMotionEvidenceWindows.AsNoTracking()
            .Where(x =>
                x.BatchId == batch.StepSensorBatchId &&
                x.BootSessionId.HasValue &&
                x.WindowStartElapsedRealtimeNs.HasValue &&
                x.WindowEndElapsedRealtimeNs.HasValue)
            .ToListAsync(cancellationToken);
        if (windows.Count == 0)
            return currentRecords.ToList();

        var bootIds = windows.Select(x => x.BootSessionId!.Value).Distinct().ToArray();
        var candidates = await _context.ValidatedStepRecords.AsNoTracking()
            .Where(x =>
                x.StepSessionId == batch.StepSessionId &&
                x.SourceCode == "step_detector" &&
                x.ClientEventId.HasValue &&
                x.BootSessionId.HasValue &&
                x.SensorElapsedRealtimeNs.HasValue &&
                bootIds.Contains(x.BootSessionId.Value))
            .ToListAsync(cancellationToken);
        var matched = candidates.Where(record => windows.Any(window =>
            window.BootSessionId == record.BootSessionId &&
            window.WindowStartElapsedRealtimeNs <= record.SensorElapsedRealtimeNs &&
            record.SensorElapsedRealtimeNs < window.WindowEndElapsedRealtimeNs));
        return currentRecords.Concat(matched)
            .DistinctBy(x => x.ValidatedStepRecordId)
            .ToList();
    }

    private sealed record DualReconciliationResult(
        IReadOnlyList<ValidatedStepRecord> SupportedCandidates,
        IReadOnlyList<ValidatedStepRecord> Resolutions,
        IReadOnlyList<CounterRecoveryShadowInterval> ShadowIntervals);

    private sealed record CounterRecoveryShadowInterval(
        Guid BootSessionId,
        long IntervalStartElapsedNs,
        long IntervalEndElapsedNs,
        long CounterFrom,
        long CounterTo,
        int CounterDelta,
        int DetectorCount,
        int SupportedDetectorCount);

    private async Task<PvpStepBatchResponse> BuildIdempotentResponseAsync(
        PvpStepSession session,
        StepSensorBatch batch,
        Guid? matchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var player = matchId.HasValue
            ? await _context.PvpMatchPlayers.AsNoTracking()
                .FirstOrDefaultAsync(x => x.MatchId == matchId && x.UserId == userId, cancellationToken)
            : null;
        var dailySnapshot = session.PurposeCode == "daily"
            ? await GetDailySnapshotAsync(userId, DateTime.UtcNow, cancellationToken)
            : null;
        return new PvpStepBatchResponse
        {
            BatchId = batch.StepSensorBatchId,
            AttestationStatus = batch.AttestationStatus,
            AcceptedSteps = batch.AcceptedSteps,
            RejectedSteps = batch.RejectedSteps,
            SuspiciousSteps = batch.SuspiciousSteps,
            NextSequence = session.LastSequence + 1,
            DailyStepDate = dailySnapshot?.Date,
            DailyAcceptedTotal = dailySnapshot?.Total,
            CurrentScore = player?.Score ?? 0,
            ValidatedSteps = player?.ValidatedSteps ?? batch.AcceptedSteps,
            DistanceUnits = player?.DistanceUnits ?? 0,
            SpeedMultiplierBps = PvpGameplayCalculator.BaseSpeedBps,
            MotionStatus = batch.MotionStatus,
            MotionScore = batch.MotionScore,
            DegradedEvidence = batch.DegradedEvidence,
            MotionReasons = JsonSerializer.Deserialize<List<string>>(batch.MotionReasonsJson) ?? []
        };
    }

    private async Task<int> AddDailyEligibleStepsAsync(
        Guid userId,
        DateTime recordedAt,
        int amount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            AsUtc(recordedAt), VietnamTimeZone));
        // A sensor batch can contain multiple events for the same Vietnam date.
        // The first event may have added this aggregate without saving it yet, so
        // check the identity map before querying the database and adding another
        // entity with the same composite key.
        var daily = _context.DailySteps.Local
            .FirstOrDefault(x => x.UserId == userId && x.StepDate == date)
            ?? await _context.DailySteps.FirstOrDefaultAsync(
                x => x.UserId == userId && x.StepDate == date,
                cancellationToken);
        if (daily == null)
        {
            daily = new DailyStep
            {
                UserId = userId,
                StepDate = date,
                StepCount = 0,
                EligibleStepCount = 0,
                UpdatedAt = now
            };
            _context.DailySteps.Add(daily);
        }
        var configured = await _context.SystemSettings.AsNoTracking()
            .Where(x => x.SettingKey == "pvp_daily_step_limit")
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
        var limit = int.TryParse(configured, out var value) && value > 0 ? value : 100000;
        var eligible = StepSensorRules.CalculateEligibleUnderDailyCap(
            daily.EligibleStepCount, amount, limit);
        checked
        {
            daily.StepCount += eligible;
            daily.EligibleStepCount += eligible;
        }
        daily.UpdatedAt = now;
        return eligible;
    }

    private async Task<DailyStepSnapshot> GetDailySnapshotAsync(
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            AsUtc(now), VietnamTimeZone));
        var daily = _context.DailySteps.Local
            .FirstOrDefault(x => x.UserId == userId && x.StepDate == date)
            ?? await _context.DailySteps.AsNoTracking().FirstOrDefaultAsync(
                x => x.UserId == userId && x.StepDate == date,
                cancellationToken);
        return new DailyStepSnapshot(
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            daily?.EligibleStepCount ?? 0);
    }

    private async Task<long> GetTotalDailyValidatedStepsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _context.ValidatedStepRecords.AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.StepSession != null &&
                x.StepSession.PurposeCode == "daily")
            .Select(x => (long?)x.EligibleStepCount)
            .SumAsync(cancellationToken) ?? 0L;
    }

    private async Task<int?> AwardPetExperienceAsync(
        Guid userId,
        int expToAdd,
        int expIncreasePerLevel,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var userPet = await _context.UserPets
            .Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (userPet == null)
            return null;

        var previousLevel = userPet.Level;
        StepExperienceReward.ApplyExperience(
            userPet,
            userPet.Pet,
            expToAdd,
            expIncreasePerLevel,
            AsUtc(now));
        return userPet.Level > previousLevel ? userPet.Level : null;
    }

    private async Task<(int ExpPerMilestone, int ExpIncreasePerLevel)> LoadProgressionSettingsAsync(
        CancellationToken cancellationToken)
    {
        var settings = await _context.SystemSettings
            .AsNoTracking()
            .Where(x => x.SettingKey == "StepToExpRate" ||
                        x.SettingKey == "PetExpIncreasePerLevel")
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, cancellationToken);

        if (!settings.TryGetValue("StepToExpRate", out var expPerMilestone) ||
            !settings.TryGetValue("PetExpIncreasePerLevel", out var expIncreasePerLevel))
            throw new AppSystemException("Pet progression settings are not configured correctly.");

        return (
            StepExperienceReward.ParseExpPerReward(expPerMilestone),
            StepExperienceReward.ParseExpIncreasePerLevel(expIncreasePerLevel));
    }

    internal static async Task SyncAcceptedProgressAsync(
        Guid userId,
        int acceptedSteps,
        int? newPetLevel,
        IAchievementProgressService achievementProgressService,
        IMissionProgressService missionProgressService)
    {
        if (acceptedSteps <= 0)
        {
            return;
        }

        await achievementProgressService.AddProgressAsync(
            userId,
            MissionMetricCodeCatalog.Steps,
            acceptedSteps);
        await missionProgressService.AddProgressAsync(
            userId,
            MissionMetricCodeCatalog.Steps,
            acceptedSteps);

        if (!newPetLevel.HasValue)
        {
            return;
        }

        await achievementProgressService.SetProgressMaxAsync(
            userId,
            MissionMetricCodeCatalog.PetLevel,
            newPetLevel.Value);
        await missionProgressService.SetProgressMaxAsync(
            userId,
            MissionMetricCodeCatalog.PetLevel,
            newPetLevel.Value);
    }

    private void AddMatchProgressEvent(
        PvpMatch match,
        PvpMatchPlayer player,
        int accepted,
        int multiplier,
        DateTime now)
    {
        var sequence = ++match.LastEventSequence;
        var payload = _timePresentationSerializer.Serialize(new
        {
            matchId = match.MatchId,
            statusCode = match.StatusCode,
            sequence,
            serverTime = now,
            details = new
            {
                playerId = player.MatchPlayerId,
                acceptedSteps = accepted,
                validatedSteps = player.ValidatedSteps,
                distanceUnits = player.DistanceUnits,
                speedMultiplierBps = multiplier
            }
        });
        _context.PvpMatchEvents.Add(new()
        {
            PvpMatchEventId = Guid.NewGuid(),
            MatchId = match.MatchId,
            Sequence = sequence,
            EventType = "match.progress",
            PayloadJson = payload,
            CreatedAt = now
        });
        _context.OutboxEvents.Add(new()
        {
            EventId = Guid.NewGuid(),
            AggregateType = "match",
            AggregateId = match.MatchId,
            Destination = "signalr",
            EventType = "match.progress",
            PayloadJson = payload,
            CreatedAt = now
        });
    }

    private static void ValidateSessionRequest(CreatePvpStepSessionRequest request)
    {
        if (request.PlatformCode != "android")
            throw new BadRequestException("This release supports Android physical-step validation only.");
        if (request.ContractVersion is not (2 or 3))
            throw new BadRequestException("Step contract version must be 2 or 3.");
        if ((request.CaptureMetadata?.GetRawText().Length ?? 0) > 8000)
            throw new BadRequestException("Capture metadata is too large.");
        _ = ResolveStoredCaptureMode(request);
    }

    private static string ResolveStoredCaptureMode(CreatePvpStepSessionRequest request)
    {
        var requested = request.ContractVersion >= 3
            ? request.CaptureMode
            : request.SensorModeCode;
        return requested switch
        {
            "dual" => "dual",
            "detector" or "detector_only" => "detector",
            "counter" or "counter_only" => "counter",
            _ => throw new BadRequestException(
                request.ContractVersion >= 3
                    ? "Capture mode must be dual, detector_only, or counter_only."
                    : "Sensor mode must be detector or counter.")
        };
    }

    private static string ToApiCaptureMode(string storedMode) => storedMode switch
    {
        "detector" => "detector_only",
        "counter" => "counter_only",
        _ => "dual"
    };

    private async Task<PvpStepSessionResponse> ToSessionResponseAsync(
        PvpStepSession session,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var dailySnapshot = session.PurposeCode == "daily"
            ? await GetDailySnapshotAsync(session.UserId, now, cancellationToken)
            : null;
        return new PvpStepSessionResponse
        {
            StepSessionId = session.StepSessionId,
            Nonce = session.Nonce,
            PurposeCode = session.PurposeCode,
            ExpiresAt = session.ExpiresAt,
            NextSequence = session.LastSequence + 1,
            ServerTime = now,
            DailyStepDate = dailySnapshot?.Date,
            DailyAcceptedTotal = dailySnapshot?.Total,
            ContractVersion = session.ContractVersion,
            CaptureMode = ToApiCaptureMode(session.SensorModeCode),
            MotionPolicy = new StepMotionPolicyResponse
            {
                ContractVersion = _motionOptions.ContractVersion,
                Required = _motionOptions.Enabled,
                WindowMilliseconds = _motionOptions.WindowMilliseconds,
                TargetSampleHz = _motionOptions.TargetSampleHz,
                MinSamplesPerWindow = _motionOptions.MinSamplesPerWindow,
                MaxSamplesPerWindow = _motionOptions.MaxSamplesPerWindow
            }
        };
    }

    private static DateTime NextVietnamDayExpiryUtc(DateTime now)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(now, VietnamTimeZone);
        var next = local.Date.AddDays(1).AddMinutes(5);
        return TimeZoneInfo.ConvertTimeToUtc(next, VietnamTimeZone);
    }

    private DateTime ResolvePvpSessionExpiry(PvpMatch match, DateTime now)
    {
        if (match.SettlementEndsAt.HasValue) return match.SettlementEndsAt.Value;
        if (match.EndedAt.HasValue)
            return match.EndedAt.Value.AddSeconds(_options.CounterSettlementSeconds + 5);
        if (match.CountdownEndsAt.HasValue)
            return match.CountdownEndsAt.Value.AddSeconds(
                match.MatchDurationSeconds + _options.CounterSettlementSeconds + 5);
        return now.AddMinutes(1);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private sealed record DailyStepSnapshot(string Date, int Total);
}
