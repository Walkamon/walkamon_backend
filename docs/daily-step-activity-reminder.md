# Daily Activity Reminder / Daily Step Goal Reminder

## 1. Overview

Daily Activity Reminder là notification khuyến khích người chơi hoàn thành mục tiêu bước chân trong ngày. Worker kiểm tra người chơi vào khoảng 18:00 theo giờ địa phương; nếu tổng bước authoritative vẫn thấp hơn mục tiêu hiện hành, hệ thống gửi tối đa một reminder cho ngày địa phương đó.

Feature này tái sử dụng notification worker, Firebase Cloud Messaging (FCM), `notifications`, `user_notifications`, `device_tokens`, `user_profiles`, `daily_steps`, `step_goals` và `system_settings` hiện có. Không có scheduler, notification table hoặc mobile background job mới.

## 2. Purpose

- Tạo một cue nhẹ nhàng để người chơi còn thời gian hoàn thành mục tiêu trong buổi tối.
- Dùng tiến độ đã được backend chấp nhận, không dùng số pending trên UI hoặc raw sensor count.
- Giữ nội dung ở phạm vi activity/game reminder, không chẩn đoán hoặc cảnh báo bệnh.

## 3. Scope

Trong scope:

- một reminder/người chơi/ngày địa phương;
- custom goal hoặc default product goal;
- giờ địa phương và grace window;
- nội dung tiếng Việt/Anh;
- persistence, retry, multi-instance idempotency và observability;
- mobile inbox nhận diện đúng type/icon.

Ngoài scope:

- thay đổi Step Detector, Step Counter, Simple Temporal Segment, Policy B, V3 reconciliation hoặc security pipeline;
- dùng pending detector để gửi reminder;
- high-step/medical warning;
- tối ưu thời điểm gửi theo cá nhân;
- bảo đảm notification làm tăng hoạt động của mọi người chơi.

## 4. Existing Step Source

Luồng Step Tracking hiện tại có nhiều mức dữ liệu: sensor evidence, pending/reconciliation state, validated records và tổng ngày. Reminder không đọc sensor evidence hay pending Flutter state. Nó đọc `daily_steps` sau khi Step validation pipeline cập nhật kết quả authoritative.

Các thành phần liên quan:

- `BLL/Service/ValidatedStepService.cs`: quyết định và ghi nhận step authoritative;
- `DAL/Models/DailyStep.cs`: tổng ngày;
- `Walkamon/BackgroundServices/NotificationSchedulerService.cs`: hosted notification worker;
- `BLL/Service/NotificationService.cs` và `DAL/Repository/NotificationRepository.cs`: notification infrastructure chung;
- `BLL/Service/FcmPushService.cs`: FCM delivery;
- `BLL/Service/DailyActivityReminderService.cs`: eligibility, claim và delivery riêng cho feature;
- `BLL/Service/DailyActivityReminderPolicy.cs`: policy thuần, testable;
- `lib/data/services/fcm_service.dart`: permission/token lifecycle trên mobile;
- `lib/screen/notifications/notifications_screen.dart`: notification inbox.

## 5. Authoritative Step Definition

`currentSteps` được lấy duy nhất từ:

```text
daily_steps.eligible_step_count
```

Không dùng:

- `daily_steps.step_count`;
- raw Counter delta;
- detector callback count;
- local pending hoặc server-pending candidates;
- suspicious/rejected evidence.

Vì vậy raw Counter tăng trong một Policy B `BLOCK`, hoặc UI đang có pending detector, không làm tăng số bước dùng cho reminder. Đây là boundary bắt buộc để reminder không ngầm bỏ qua anti-cheat/authoritative policy.

## 6. Daily Step Goal

Goal được chọn theo thứ tự:

1. Bản ghi `step_goals` mới nhất có `effective_from <= localDate` của người chơi và `target_steps > 0`.
2. Nếu không có, dùng `daily_activity_reminder_default_goal` trong `system_settings`.
3. Nếu setting thiếu/sai, code fallback an toàn về `7000`.

Default không ghi đè custom goal. `7000` chỉ được seed một lần trong database và có một fallback constant để chống configuration hỏng; business code không rải số này ở nhiều nơi.

## 7. Why 7000 Steps

Phân loại: **PRODUCT DECISION, được hỗ trợ bởi ACADEMIC EVIDENCE**.

7000 bước/ngày không phải minimum medical requirement. Walkamon chọn nó làm default có tính khả thi và có evidence hỗ trợ. Systematic review và dose-response meta-analysis năm 2025 tổng hợp 57 nghiên cứu từ 35 cohort; 31 nghiên cứu từ 24 cohort được đưa vào meta-analysis. So với 2000 bước/ngày, 7000 bước/ngày *liên quan với* nhiều kết quả sức khỏe tốt hơn, nhưng tác giả cũng nêu các hạn chế gồm số nghiên cứu nhỏ cho nhiều outcome, thiếu phân tích đặc thù theo tuổi, bias ở nghiên cứu thành phần và residual confounding [3]. Đây là association, không phải bằng chứng rằng 7000 bước tự nó gây ra một mức giảm rủi ro cố định.

CARDIA cũng ghi nhận ở người trưởng thành trung niên rằng nhóm đạt khoảng từ 7000 bước/ngày có mortality thấp hơn trong dữ liệu cohort [4]. Nghiên cứu quan sát này không chứng minh quan hệ nhân quả.

Meta-analysis 15 cohort năm 2022 cho thấy dose-response khác theo tuổi: mức giảm rủi ro có xu hướng plateau khoảng 6000–8000 bước/ngày ở nhóm từ 60 tuổi và khoảng 8000–10000 ở người trẻ hơn [5]. Các khoảng này không phải hard cutoff và không phù hợp để Walkamon tạo cảnh báo y tế.

## 8. Official Physical Activity Guidelines

Phân loại: **OFFICIAL GUIDANCE**.

WHO khuyến nghị người trưởng thành thực hiện 150–300 phút hoạt động aerobic mức vừa hoặc 75–150 phút mức mạnh mỗi tuần, cùng hoạt động tăng cường cơ [1]. CDC nêu ít nhất 150 phút hoạt động mức vừa mỗi tuần và thông điệp “move more, sit less” [2].

Hai guideline này chủ yếu dùng thời lượng và cường độ; chúng không quy định 7000 bước/ngày là universal official minimum. Walkamon không mô tả default 7000 là khuyến nghị chính thức của WHO hoặc CDC.

## 9. Academic Evidence

Phân loại: **ACADEMIC EVIDENCE**.

- Ding et al. là evidence chính để chọn một default có cơ sở, nhưng kết quả là association và còn residual confounding [3].
- Paluch et al. (CARDIA) bổ sung cohort evidence về mức khoảng 7000 ở người trung niên, không phải causal trial [4].
- Paluch et al. (15 cohort) cho thấy quan hệ theo tuổi và không ủng hộ một cutoff giống nhau cho mọi người [5].

Do đó 7000 là starting product default, không phải prescription cá nhân. Người chơi có thể đặt custom goal phù hợp trong giới hạn sản phẩm.

## 10. Evidence for Mobile Activity Reminders

Phân loại: **ACADEMIC EVIDENCE**, không phải bảo đảm hiệu quả của implementation Walkamon.

Goal setting, self-monitoring, prompts/cues và mobile messaging thường cùng xuất hiện trong digital physical-activity interventions. HeartSteps thử nghiệm context-aware walking suggestions qua smartphone với 44 người trưởng thành. Walking suggestions có thể tăng bước trong khoảng ngắn sau quyết định gửi, nhưng hiệu ứng thay đổi theo thời gian và giảm dần trong trial [6]. HeartSteps không chứng minh 18:00 là giờ tối ưu.

Umbrella review năm 2024 tổng hợp 47 meta-analysis, 507 RCT và 206.873 người tham gia. eHealth/mHealth interventions liên quan với cải thiện trung bình về nhiều hành vi, gồm khoảng 1329 bước/ngày ở pooled post-intervention result [7]. Intervention là tổ hợp app, web, SMS và nhiều behavior-change technique; không được quy toàn bộ effect cho push notification.

Counter-evidence cũng quan trọng: microrandomized trial công bố năm 2026 trên người tăng huyết áp không tìm thấy mức tăng có ý nghĩa thống kê ở step count trong 60 phút sau activity push notification (estimate 1.01, 95% CI 0.98–1.04, `P=.40`) [8]. Vì vậy reminder là một cue có khả năng hữu ích, không phải cơ chế chắc chắn làm người dùng vận động thêm.

## 11. Why 18:00

Phân loại: **PRODUCT DECISION**.

18:00 không phải thời điểm WHO, CDC hoặc Lancet khuyến nghị. Walkamon chọn 18:00 vì:

- đủ muộn để phần lớn activity trong ngày đã được ghi nhận;
- vẫn còn thời gian buổi tối để hoàn thành goal;
- không nhắc quá sớm khi người chơi chưa có cơ hội hoạt động;
- tránh gửi quá khuya.

Default window là `[18:00, 20:00)` theo giờ địa phương: bắt đầu inclusive, kết thúc exclusive. Polling worker có thể bắt kịp trong 120 phút nếu tạm dừng, nhưng restart lúc 23:30 không gửi bù reminder 18:00. Thời gian và grace được cấu hình trong `system_settings`, nên kiến trúc không khóa cứng 18:00 cho tương lai.

## 12. Notification Decision Flow

```text
worker tick (UTC)
  -> feature enabled?
  -> account active and role User?
  -> resolve user timezone and local date/time
  -> inside [local reminder time, reminder time + grace)?
  -> notifications_enabled?
  -> authoritative daily_steps.eligible_step_count
  -> latest active custom goal, else system default
  -> currentSteps < dailyGoal?
  -> at least one active device token?
  -> deterministic daily identity already sent/retry-leased?
  -> claim in SQL transaction
  -> localized FCM send
  -> sent or failed (retry after lease while still in window)
```

Formulas:

```text
remainingSteps = max(dailyGoal - currentSteps, 0)
identity = SHA-256(notificationType | userId | localDate) -> GUID
```

## 13. Timezone Handling

- `user_profiles.time_zone_id` là nguồn chính.
- `DateTimeOffset` UTC từ `TimeProvider` được convert bằng `TimeZoneInfo`.
- Local date điều khiển cả `daily_steps`, goal effective date và idempotency key.
- Runtime hỗ trợ IANA/Windows IDs theo platform data hiện có.
- Nếu timezone thiếu hoặc invalid, fallback lần lượt về `Asia/Ho_Chi_Minh`, `SE Asia Standard Time`, rồi UTC nếu OS không có cả hai; fallback được log bằng user ID nhưng không log token.
- Người ở timezone khác nhau được đánh giá theo 18:00 của chính họ, không phải 18:00 server.

Fallback Việt Nam là quyết định tương thích với default architecture hiện tại, không phải Android/medical requirement.

## 14. Idempotency

Một người chơi có tối đa một persisted reminder cho `(daily_step_goal_reminder, userId, localDate)`:

- notification ID deterministic từ SHA-256 identity;
- `notifications.notification_id` chặn duplicate vật lý;
- `user_notifications` liên kết đúng người nhận;
- `sys.sp_getapplock` transaction lock chặn hai backend instance cùng claim;
- trạng thái `sent` là terminal cho ngày đó;
- `scheduled/failed` mới hơn retry lease 5 phút bị defer;
- Android FCM dùng deterministic `collapse_key`, notification `tag` và `deliveryKey` để giảm duplicate hiển thị.

Worker broadcast chung loại `target_audience_code='single_user'` khỏi due-query. Điều này ngăn một reminder đang chờ retry bị dispatch nhầm cho toàn bộ audience.

Idempotency database bảo đảm không tạo nhiều logical reminder/side effect. Nó không thể tạo exactly-once delivery tuyệt đối qua một network API: nếu FCM đã nhận message nhưng response bị mất, retry vẫn có thể xảy ra. Stable Android tag/collapse key làm retry thay thế cùng notification thay vì tạo nhiều card trên Android khi FCM tôn trọng semantics đó.

## 15. Notification Content

Tiếng Việt:

```text
Title: Cùng đi thêm một chút nhé! 🌱
Body: Hôm nay bạn đã đi {currentSteps} bước. Còn {remainingSteps} bước nữa để đạt mục tiêu {dailyGoal} bước.
```

English:

```text
Title: A few more steps today! 🌱
Body: You've walked {currentSteps} steps today. Only {remainingSteps} more to reach your {dailyGoal}-step goal.
```

Các con số được format theo locale. Nội dung không nói người chơi “phải” đạt 7000, không gắn bước chân với chẩn đoán hoặc disease risk.

## 16. Localization

- Backend chọn Việt/Anh theo `user_profiles.language_code`; `vi*` dùng tiếng Việt, các giá trị còn lại fallback English.
- Mobile inbox map `daily_step_goal_reminder` sang label ARB tiếng Việt/Anh và dùng icon bước chân.
- FCM title/body được backend render vì notification có thể hiển thị khi app không chạy.

## 17. Failure / Retry Handling

- Feature mặc định được cài với `daily_activity_reminder_enabled=false`; operator bật sau khi worker và Firebase credentials sẵn sàng.
- Không cấu hình FCM hoặc send lỗi: record thành `failed`, tăng failure metric, retry sau lease 5 phút nếu vẫn trong local window.
- Token invalid/unregistered bị deactivate; token value không xuất hiện trong log.
- Không còn active token tại delivery: record `failed`, metric `missingTokenSkipped` tăng.
- Worker hoặc server restart: state được tái dựng từ database; không dùng in-memory sent flag.
- Retry sau khi window đóng không xảy ra, nên không gửi bù muộn lúc 23:30.
- Notification đã `sent` không gửi lại trong cùng local date.

## 18. Security and Privacy

- Reminder chỉ đọc user ID, preference, locale/timezone, active-token existence, goal và authoritative daily aggregate cần thiết.
- Không đọc/đưa raw motion, Integrity token, JWT, nonce hoặc sensor timeline vào notification.
- Log dùng notification ID/device-token row ID; không log FCM token đầy đủ.
- SQL parameterization/EF Core và transaction application lock được dùng cho claim.
- User opt-out (`notifications_enabled=false`) được kiểm tra trước khi tạo notification.

## 19. Medical / Health Claim Boundaries

Reminder là encouragement để hoàn thành game/activity goal. Nó không phải:

- medical warning hoặc disease-risk alert;
- chẩn đoán sức khỏe;
- khuyến nghị điều trị;
- tuyên bố 7000 là minimum để khỏe mạnh;
- tuyên bố không đạt goal gây bệnh.

Trong product copy và analytics phải dùng ngôn ngữ “mục tiêu”, “khuyến khích” và “liên quan với” khi mô tả evidence; không biến association thành causation.

## 20. Why No High-Step Warning

Không có universal official maximum daily step count phù hợp để Walkamon biến 15.000, 20.000 hoặc 30.000 bước thành hard health warning. High-step achievement có thể là game feature riêng, nhưng không thuộc reminder này và không được mô tả như medical safety limit.

## 21. Test Cases

Automated policy/SQL tests bao phủ:

| # | Scenario | Expected |
|---|---|---|
| 1 | 17:59, goal 7000, authoritative 3000 | chưa gửi |
| 2 | 18:00, goal 7000, authoritative 3000 | gửi đúng một, remaining 4000 |
| 3 | authoritative 6999 | gửi, remaining 1 |
| 4 | authoritative 7000 | không gửi |
| 5 | authoritative 10000 | không gửi |
| 6 | custom goal 10000, authoritative 7500 | gửi, remaining 2500 |
| 7 | custom goal 5000, authoritative 6000 | không gửi |
| 8 | worker chạy lặp | một notification/một FCM success |
| 9 | hai service instance đồng thời | một DB identity/một send |
| 10 | FCM failure rồi retry | không thêm DB row; retry sau lease |
| 11 | notification disabled | không gửi |
| 12 | thiếu active token | skip và metric đúng |
| 13 | timezone khác | eligibility theo 18:00 local |
| 14 | local-date rollover | identity ngày mới độc lập |
| 15 | raw/pending count cao, eligible thấp | dùng `eligible_step_count` |
| 16 | Policy B block làm raw tăng, eligible không tăng | vẫn dùng eligible authoritative |
| 17 | 20:00 hoặc 23:30 | window đóng, không catch-up muộn |
| 18 | invalid timezone | fallback và diagnostic |
| 19 | single-user claim ở trạng thái scheduled | worker chung không broadcast |

Quality gate cần chạy:

```powershell
dotnet test .\Walkamon.IntegrationTests\Walkamon.IntegrationTests.csproj --filter "FullyQualifiedName~DailyActivityReminder"
dotnet test .\Walkamon.IntegrationTests\Walkamon.IntegrationTests.csproj --filter "FullyQualifiedName~StepTracking"
dotnet test .\Walkamon.sln -c Release
dotnet build .\Walkamon.sln -c Release --no-restore

flutter gen-l10n
flutter analyze
flutter test
flutter build apk --debug
```

## 22. Limitations

- 7000 là default quần thể mang tính sản phẩm, không phải goal cá nhân hóa theo tuổi, khả năng vận động hoặc tình trạng sức khỏe.
- 18:00 chưa được thử nghiệm A/B trong Walkamon và không có evidence cho một giờ tối ưu universal.
- Polling cadence làm thời điểm thực tế lệch tối đa khoảng một tick worker trong window.
- Database idempotency không thể chứng minh exactly-once hiển thị qua FCM trong mọi network failure; Android tag/collapse chỉ giảm duplicate.
- Fallback timezone có thể không phản ánh vị trí hiện tại nếu profile thiếu/sai; metric phải được theo dõi và profile nên được sửa.
- Digital interventions có average effect ở cấp nghiên cứu, nhưng không bảo đảm reminder này hiệu quả với từng người [6]–[8].
- Locale hiện hỗ trợ nội dung reminder tiếng Việt/Anh; locale khác fallback English.

## 23. References

[1] World Health Organization, “Physical activity,” WHO Regional Office for Europe, Sep. 1, 2021. [Online]. Available: https://www.who.int/europe/news-room/fact-sheets/item/physical-activity. [Accessed: Aug. 17, 2026].

[2] U.S. Centers for Disease Control and Prevention, “Adult Activity: An Overview,” Dec. 20, 2023. [Online]. Available: https://www.cdc.gov/physical-activity-basics/guidelines/adults.html. [Accessed: Aug. 17, 2026].

[3] D. Ding, B. Nguyen, T. Nau, *et al*., “Daily steps and health outcomes in adults: a systematic review and dose-response meta-analysis,” *The Lancet Public Health*, vol. 10, no. 8, pp. e668–e681, Aug. 2025, doi: 10.1016/S2468-2667(25)00164-1. [Online]. Available: https://pubmed.ncbi.nlm.nih.gov/40713949/.

[4] A. E. Paluch, K. P. Gabriel, J. E. Fulton, *et al*., “Steps per Day and All-Cause Mortality in Middle-aged Adults in the Coronary Artery Risk Development in Young Adults Study,” *JAMA Network Open*, vol. 4, no. 9, Art. no. e2124516, Sep. 2021, doi: 10.1001/jamanetworkopen.2021.24516. [Online]. Available: https://jamanetwork.com/journals/jamanetworkopen/fullarticle/2783711.

[5] A. E. Paluch, S. Bajpai, D. R. Bassett, *et al*., “Daily steps and all-cause mortality: a meta-analysis of 15 international cohorts,” *The Lancet Public Health*, vol. 7, no. 3, pp. e219–e228, Mar. 2022, doi: 10.1016/S2468-2667(21)00302-9. [Online]. Available: https://pmc.ncbi.nlm.nih.gov/articles/PMC9289978/.

[6] P. Klasnja, S. Smith, N. J. Seewald, *et al*., “Efficacy of Contextually Tailored Suggestions for Physical Activity: A Micro-randomized Optimization Trial of HeartSteps,” *Annals of Behavioral Medicine*, vol. 53, no. 6, pp. 573–582, Jun. 2019, doi: 10.1093/abm/kay067. [Online]. Available: https://academic.oup.com/abm/article/53/6/573/5091257.

[7] B. Singh, M. Ahmed, A. E. Staiano, *et al*., “A systematic umbrella review and meta-meta-analysis of eHealth and mHealth interventions for improving lifestyle behaviours,” *npj Digital Medicine*, vol. 7, Art. no. 179, Jul. 2024, doi: 10.1038/s41746-024-01172-y. [Online]. Available: https://www.nature.com/articles/s41746-024-01172-y.

[8] J. R. Golbus, M. P. Dorsch, Y. Chen, *et al*., “Impact of Push Notifications on Physical Activity and Sodium Intake Among Patients with Hypertension: Microrandomized Trial of a Just-in-Time Adaptive Intervention,” *Journal of Medical Internet Research*, vol. 28, Art. no. e78218, Jan. 2026, doi: 10.2196/78218. [Online]. Available: https://www.jmir.org/2026/1/e78218/.

All URLs and DOI/title mappings above were checked on Aug. 17, 2026. Publisher access restrictions may vary; PubMed/PMC links are retained where available.
