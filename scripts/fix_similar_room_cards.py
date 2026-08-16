from pathlib import Path

path = Path('src/DeLong.Web/Pages/Rooms/Details.cshtml')
text = path.read_text(encoding='utf-8')

old_media = '''@if (!string.IsNullOrWhiteSpace(room.CoverCardUrl)) { <img class="public-room-cover-image" src="@room.CoverCardUrl" alt="@room.Name" loading="lazy" /><span class="public-room-image-scrim"></span> } else { <span class="public-room-poster"><small>@Model.PropertyName · @room.Code</small><strong>@room.Name</strong></span> }<span class="public-room-image-title"><small>@Model.PropertyName</small><strong>@room.Name</strong></span>'''
new_media = '''@if (!string.IsNullOrWhiteSpace(room.CoverCardUrl)) { <img class="public-room-cover-image" src="@room.CoverCardUrl" alt="@room.Name" loading="lazy" /><span class="public-room-image-scrim"></span><span class="public-room-image-title"><small>@Model.PropertyName</small><strong>@room.Name</strong></span> } else { <span class="public-room-poster"><small>@Model.PropertyName · @room.Code</small><strong>@room.Name</strong></span> }'''

if text.count(old_media) != 1:
    raise SystemExit(f'expected exactly one similar-room media block, found {text.count(old_media)}')
text = text.replace(old_media, new_media, 1)

old_cta = '>Đặt ngay →</a>'
if text.count(old_cta) != 1:
    raise SystemExit(f'expected exactly one similar-room CTA, found {text.count(old_cta)}')
text = text.replace(old_cta, '>Đặt ngay</a>', 1)

path.write_text(text, encoding='utf-8')
