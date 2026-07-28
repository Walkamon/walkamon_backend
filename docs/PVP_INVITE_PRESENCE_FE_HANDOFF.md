# FE handoff — PvP presence, friend list và invite

Phạm vi gồm trạng thái PvP trong friend list, lời mời thách đấu và event presence realtime.

## 1. Kết nối SignalR trước khi dùng invite

FE phải kết nối và authenticate hub trước khi bật nút gửi/chấp nhận:

```text
/hubs/pvp-sprint
```

Đăng ký mọi handler trước khi gọi `start()`. Nếu hub chưa ở trạng thái connected:

- Không cho gửi invite.
- Không cho accept invite.
- Có thể cho reject invite vì reject không yêu cầu người gửi còn online.

Backend vẫn kiểm tra lại trạng thái thật. Việc disable nút ở FE chỉ để cải thiện UX.

## 2. Trạng thái PvP trong friend list

```http
GET /api/friends
Authorization: Bearer <JWT>
```

Mỗi friend có thêm:

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "email": "friend@example.com",
  "username": "friend",
  "avatarUrl": "https://...",
  "bio": "...",
  "isOnline": true,
  "pvpAvailabilityCode": "available"
}
```

Nguồn dữ liệu:

- `isOnline`: user có connection trong `PvpPresenceTracker`.
- `pvpAvailabilityCode == offline`: không có SignalR connection.
- `pvpAvailabilityCode == busy`: online và có row trong `pvp_player_activities`.
- `pvpAvailabilityCode == available`: online và không có row trong `pvp_player_activities`.

Hai field này được backend tính khi trả response, không phải cột trong database.

FE chỉ bật nút thách đấu khi:

```text
friend.isOnline == true
friend.pvpAvailabilityCode == available
SignalR của chính user đang connected
```

Friend list là snapshot tại thời điểm gọi REST. Sau đó FE dùng `presence.changed` để cập nhật card tương ứng. Khi reconnect, gọi lại `GET /api/friends`.

Lưu ý: friend đang có pending invite sẽ là `busy` trong friend list vì đang giữ activity lock. Trong chính invite card, người đó vẫn có thể là `available` đối với invite pending đó.

## 3. Contract của invite

Mỗi item từ `GET /api/pvp/sprint/invites` và response create/respond có thêm:

```json
{
  "inviteId": "00000000-0000-0000-0000-000000000000",
  "user": {
    "userId": "00000000-0000-0000-0000-000000000000",
    "username": "friend",
    "avatarUrl": "https://..."
  },
  "otherUserIsOnline": true,
  "otherUserPvpAvailabilityCode": "available",
  "statusCode": "pending",
  "expiresAt": "2026-07-28T18:31:00+07:00",
  "createdAt": "2026-07-28T18:30:00+07:00",
  "matchId": null
}
```

`otherUser...` luôn nói về user trong object `user`, không phải user đang đăng nhập.

Giá trị availability:

| Giá trị | Ý nghĩa |
|---|---|
| `available` | Online và có thể tiếp tục đúng invite này. |
| `busy` | Online nhưng đang có PvP activity khác. |
| `offline` | Không có SignalR connection hoạt động. |

Với invite đang pending, activity `invite_pending` của chính invite đó vẫn được trả là `available`, không phải `busy`.

## 4. Gửi, accept và reject

### Gửi invite

```http
POST /api/pvp/sprint/invites
```

Backend yêu cầu cả người gửi và người nhận đang online trên SignalR, là bạn bè và không bận. `409` nghĩa là trạng thái đã thay đổi; FE dừng loading, hiển thị message và refresh danh sách invite.

### Accept

```http
POST /api/pvp/sprint/invites/{inviteId}/response
Content-Type: application/json

{ "accept": true }
```

Chỉ enable nút khi:

```text
statusCode == pending
otherUserIsOnline == true
otherUserPvpAvailabilityCode == available
expiresAt > server time
SignalR của chính user đang connected
```

Backend kiểm tra lại người gửi còn online và hai activity lock vẫn thuộc invite. Khi HTTP trả `200`, FE phải dừng loading ngay và dùng `data.matchId`; không chờ thêm event SignalR. `409` thì refresh invite vì người gửi có thể vừa offline hoặc trạng thái vừa thay đổi.

### Reject

```http
POST /api/pvp/sprint/invites/{inviteId}/response
Content-Type: application/json

{ "accept": false }
```

Reject không yêu cầu inviter còn online. Khi HTTP trả `200`, FE dừng loading và cập nhật card thành `declined`; không chờ SignalR.

Retry cùng lựa chọn là idempotent. Retry accept/reject đã thành công trước đó không tạo thêm match hoặc side effect.

## 5. Event `presence.changed`

Đăng ký handler:

```text
presence.changed
```

Envelope:

```json
{
  "eventId": "00000000-0000-0000-0000-000000000000",
  "eventType": "presence.changed",
  "aggregateId": "user-id-thay-doi",
  "payload": {
    "userId": "user-id-thay-doi",
    "isOnline": false,
    "pvpAvailabilityCode": "offline",
    "serverTime": "2026-07-28T18:30:00+07:00"
  }
}
```

FE dùng `payload.userId` để cập nhật friend card có `friend.userId` và mọi invite card có `item.user.userId` tương ứng. Dedupe theo `eventId`. Khi reconnect, luôn gọi lại `GET /api/friends` và `GET /api/pvp/sprint/invites`; event chỉ dùng để cập nhật realtime, REST vẫn là nguồn dữ liệu authoritative.

Một user có nhiều thiết bị vẫn online cho tới khi connection cuối cùng đóng. Khi app bị kill hoặc mất mạng đột ngột, trạng thái offline chỉ phát sau khi SignalR phát hiện disconnect nên có thể không tức thời.

## 6. Những điểm FE không được làm

- Không suy ra online từ lần cuối mở app.
- Không chỉ dựa vào trạng thái đang hiển thị để quyết định nghiệp vụ; luôn xử lý `409`.
- Không giữ spinner để chờ `presence.changed`, `match.assigned` hoặc event invite sau khi HTTP đã trả.
- Không parse thời gian bằng cách tự cộng/trừ 7 giờ; timestamp đã có offset `+07:00`.
- Không tự đặt user thành `available` chỉ vì không thấy match trên UI; dùng field backend trả về.

## 7. Kết thúc trận và popup kết quả

Backend đã sửa thứ tự settlement để không phát `match.finished` khi kết quả chưa tồn tại:

```text
Race hết giờ
→ chốt distance cuối
→ chuyển settling ngay, không chờ thêm 10 giây
→ tính winner/draw
→ tính MMR
→ tạo reward entitlement
→ lưu statusCode = finished
→ ghi match.finished vào transactional outbox
→ commit transaction
→ outbox dispatcher mới phát SignalR match.finished
```

Vì outbox chưa thể được đọc trước khi transaction commit, khi FE nhận `match.finished` thì `GET /api/pvp/sprint/matches/{matchId}/result` đã sẵn sàng. Thời gian chuyển từ hết race sang finished bình thường chỉ còn khoảng một nhịp lifecycle worker, không còn khoảng chờ settlement cố định 8–15 giây.

### FE phải xử lý

- Không gọi result chỉ vì đồng hồ local vừa về `0`.
- Không gọi result khi nhận `match.settling`; đây chỉ là trạng thái chuyển tiếp.
- Khi nhận `match.finished`, gọi GET result ngay và hiển thị popup.
- Dedupe `match.finished` theo `eventId` và sequence để không mở popup hai lần.
- Sau reconnect, gọi `GET /api/pvp/sprint/matches/{matchId}`:
  - `statusCode == finished`: gọi GET result ngay.
  - `statusCode == settling`: chờ `match.finished` hoặc poll GET match mỗi 1 giây.
  - `statusCode == cancelled`: đóng loading và xử lý màn trận bị hủy.
- Nếu GET result vẫn trả `409` trước khi nhận `match.finished`, dừng gọi result liên tục; quay lại GET match để chờ trạng thái authoritative.
- Không giữ popup loading vô hạn. Nếu mất SignalR, fallback bằng GET match; nút “Tiếp tục” có thể đóng popup và cho user xem lại kết quả từ lịch sử.

Luồng FE khuyến nghị:

```text
Local race timer = 0
→ hiện “Đang chốt kết quả”
→ nhận match.settling: giữ trạng thái chờ
→ nhận match.finished
→ GET /result
→ HTTP 200: tắt spinner và render kết quả
```
