# Adds Google Calendar private iCal addresses to %APPDATA%\DesktopCalendar\calendars.json.
# Run: powershell -ExecutionPolicy Bypass -File scripts\add-calendar.ps1
$ErrorActionPreference = 'Stop'
$root = Join-Path $env:APPDATA 'DesktopCalendar'
$path = Join-Path $root 'calendars.json'
$cache = Join-Path $root 'cache'
New-Item -ItemType Directory -Force $root, $cache | Out-Null
if (-not (Test-Path $path)) {
    [IO.File]::WriteAllText($path, '[]', (New-Object Text.UTF8Encoding $false))
}

# The addresses grant read access to the calendars, so only this account, SYSTEM and
# Administrators may read them; sandboxed agents inherit no access from the profile.
$user = '*' + [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
icacls $path /inheritance:r /grant:r "${user}:F" '*S-1-5-18:F' '*S-1-5-32-544:F' | Out-Null
icacls $cache /inheritance:r /grant:r "${user}:(OI)(CI)F" '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
if (Get-ChildItem $cache) { icacls (Join-Path $cache '*') /reset | Out-Null }

$list = @()
$text = (Get-Content $path -Raw -Encoding UTF8) -replace '(?m)^\s*//.*$', ''
if ($text.Trim()) { $list = @((ConvertFrom-Json $text) | ForEach-Object { $_ }) }

Write-Host ''
Write-Host "현재 등록된 캘린더: $($list.Count)개"
Write-Host 'Google 캘린더 설정 > 캘린더 통합 > "iCal 형식의 비공개 주소"를 복사해 붙여 넣으세요.'
Write-Host '(붙여 넣기: 마우스 오른쪽 클릭 또는 Ctrl+V, 끝내려면 그냥 Enter)'

while ($true) {
    Write-Host ''
    $url = "$(Read-Host '주소')".Trim()
    if (-not $url) { break }
    if ($url -notmatch '^https://calendar\.google\.com/calendar/ical/.+\.ics$') {
        Write-Host '  iCal 주소 형식이 아닙니다. "https://calendar.google.com/calendar/ical/"로 시작하고 ".ics"로 끝나야 합니다.' -ForegroundColor Yellow
        continue
    }
    if ($list | Where-Object { $_.Url -eq $url }) {
        Write-Host '  이미 등록된 주소입니다.' -ForegroundColor Yellow
        continue
    }
    $color = "$(Read-Host '색 (예: #8AB4F8, 그냥 Enter면 기본색)')".Trim()
    if (-not $color) { $color = '#8AB4F8' }
    $list += [pscustomobject]@{ Url = $url; Color = $color }
    [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject @($list)), (New-Object Text.UTF8Encoding $false))
    Write-Host "  추가했습니다. 지금 $($list.Count)개입니다." -ForegroundColor Green
}

Write-Host ''
Write-Host '끝났습니다. 달력 앱에서 우클릭 > 새로 고침을 누르거나, 앱을 다시 실행하세요.'
