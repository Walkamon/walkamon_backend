using System.Data;
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
using Microsoft.Extensions.Options;

namespace BLL.Service;

public sealed class ValidatedStepService : IValidatedStepService
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    private readonly WalkamonContext _context;
    private readonly IAppAttestationVerifier _attestationVerifier;
    private readonly StepValidationOptions _options;
    private readonly MotionValidationOptions _motionOptions;

    public ValidatedStepService(
        WalkamonContext context,
        IAppAttestationVerifier attestationVerifier,
        IOptions<StepValidationOptions> options,
        IOptions<MotionValidationOptions> motionOptions)
    {
        _context = context;
        _attestationVerifier = attestationVerifier;
        _options = options.Value;
        _motionOptions = motionOptions.Value;
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
        var reusable = active.FirstOrDefault(x =>
            x.PurposeCode == purposeCode && x.MatchId == matchId && x.ExpiresAt > now);
        if (reusable != null)
        {
            return ToSessionResponse(reusable, now);
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
            SensorModeCode = request.SensorModeCode,
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            StatusCode = "active",
            ExpiresAt = expiresAt,
            CreatedAt = now
        };
        _context.PvpStepSessions.Add(created);
        await _context.SaveChangesAsync(cancellationToken);
        return ToSessionResponse(created, now);
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
        if (request.Events.Count is < 1 or > 25)
            throw new BadRequestException("A step batch must contain between 1 and 25 events.");
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
        if (verifiedSessionBatch != null)
        {
            attestation = new(
                true,
                "session_cached",
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

            if (eligible > 0 && player != null)
            {
                lastMultiplier = session.SensorModeCode == "counter"
                    ? StepSensorRules.MinimumPvpMultiplier(
                        match!, player, item.IntervalStartedAt, item.RecordedAt, effects)
                    : StepSensorRules.MinimumPvpMultiplier(
                        match!, player, item.RecordedAt, item.RecordedAt, effects);
                var distance = PvpGameplayCalculator.CalculateDistanceUnits(eligible, lastMultiplier);
                accepted += eligible;
                distanceAdded = checked(distanceAdded + distance);
                _context.PvpMatchStepLedgers.Add(new PvpMatchStepLedger
                {
                    MatchStepLedgerId = Guid.NewGuid(),
                    MatchId = match!.MatchId,
                    MatchPlayerId = player.MatchPlayerId,
                    ValidatedStepRecordId = record.ValidatedStepRecordId,
                    CountedSteps = eligible,
                    MultiplierBps = lastMultiplier,
                    DistanceUnits = distance,
                    EffectSnapshotJson = JsonSerializer.Serialize(effects
                        .Where(x => x.StartsAt <= item.RecordedAt && (x.ConsumedAt ?? x.EndsAt) > item.IntervalStartedAt)
                        .Select(x => new { x.EffectCode, x.EffectKindCode, x.MagnitudeBps, x.StartsAt, x.EndsAt })),
                    CreatedAt = now
                });
            }
            if (rule.IsEligible && item.SensorEndTotal.HasValue)
                rollingSensorTotal = item.SensorEndTotal;
            if (rule.IsEligible)
                session.LastRecordedAt = item.RecordedAt;
        }

        if (player != null)
        {
            player.ValidatedSteps = checked(player.ValidatedSteps + accepted);
            player.DistanceUnits = checked(player.DistanceUnits + distanceAdded);
            player.Score = (int)Math.Min(
                int.MaxValue,
                player.DistanceUnits / PvpGameplayCalculator.DistanceUnitsPerStep);
            AddMatchProgressEvent(match!, player, accepted, lastMultiplier, now);
        }
        session.LastSequence = request.Sequence;
        session.LastSubmittedAt = now;
        session.LastSensorTotal = rollingSensorTotal;
        await _context.SaveChangesAsync(cancellationToken);
        return new PvpStepBatchResponse
        {
            BatchId = batch.StepSensorBatchId,
            AttestationStatus = batch.AttestationStatus,
            AcceptedSteps = batch.AcceptedSteps,
            RejectedSteps = batch.RejectedSteps,
            SuspiciousSteps = batch.SuspiciousSteps,
            NextSequence = session.LastSequence + 1,
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
        return new PvpStepBatchResponse
        {
            BatchId = batch.StepSensorBatchId,
            AttestationStatus = batch.AttestationStatus,
            AcceptedSteps = batch.AcceptedSteps,
            RejectedSteps = batch.RejectedSteps,
            SuspiciousSteps = batch.SuspiciousSteps,
            NextSequence = session.LastSequence + 1,
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

    private void AddMatchProgressEvent(
        PvpMatch match,
        PvpMatchPlayer player,
        int accepted,
        int multiplier,
        DateTime now)
    {
        var sequence = ++match.LastEventSequence;
        var payload = JsonSerializer.Serialize(new
        {
            matchId = match.MatchId,
            status = match.StatusCode,
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
        if (request.SensorModeCode is not ("detector" or "counter"))
            throw new BadRequestException("Sensor mode must be detector or counter.");
    }

    private PvpStepSessionResponse ToSessionResponse(PvpStepSession session, DateTime now) => new()
    {
        StepSessionId = session.StepSessionId,
        Nonce = session.Nonce,
        PurposeCode = session.PurposeCode,
        ExpiresAt = session.ExpiresAt,
        NextSequence = session.LastSequence + 1,
        ServerTime = now,
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

    private static DateTime NextVietnamDayExpiryUtc(DateTime now)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(now, VietnamTimeZone);
        var next = local.Date.AddDays(1).AddMinutes(5);
        return TimeZoneInfo.ConvertTimeToUtc(next, VietnamTimeZone);
    }

    private static DateTime ResolvePvpSessionExpiry(PvpMatch match, DateTime now)
    {
        if (match.SettlementEndsAt.HasValue) return match.SettlementEndsAt.Value;
        if (match.EndedAt.HasValue) return match.EndedAt.Value.AddSeconds(10);
        if (match.CountdownEndsAt.HasValue) return match.CountdownEndsAt.Value.AddSeconds(40);
        return now.AddMinutes(1);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
