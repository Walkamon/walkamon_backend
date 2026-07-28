# FE handoff — PvP invite presence

Phạm vi thay đổi này chỉ áp dụng cho lời mời PvP. `GET /api/friends` không thay đổi.

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

## 2. Contract của invite

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

## 3. Gửi, accept và reject

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

## 4. Event `presence.changed`

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

FE dùng `payload.userId` để cập nhật mọi invite card có `item.user.userId` tương ứng. Dedupe theo `eventId`. Khi reconnect, luôn gọi lại `GET /api/pvp/sprint/invites`; event chỉ dùng để cập nhật realtime, REST vẫn là nguồn dữ liệu authoritative.

Một user có nhiều thiết bị vẫn online cho tới khi connection cuối cùng đóng. Khi app bị kill hoặc mất mạng đột ngột, trạng thái offline chỉ phát sau khi SignalR phát hiện disconnect nên có thể không tức thời.

## 5. Những điểm FE không được làm

- Không suy ra online từ lần cuối mở app.
- Không chỉ dựa vào trạng thái đang hiển thị để quyết định nghiệp vụ; luôn xử lý `409`.
- Không giữ spinner để chờ `presence.changed`, `match.assigned` hoặc event invite sau khi HTTP đã trả.
- Không parse thời gian bằng cách tự cộng/trừ 7 giờ; timestamp đã có offset `+07:00`.
- Không thay đổi model của `GET /api/friends` trong đợt này.
