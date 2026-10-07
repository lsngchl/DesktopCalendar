# Desktop Calendar

Windows 바탕화면에 붙는 독립형 WPF 달력 앱이다. Rainmeter 없이 실행되며, `Progman`을 owner로 둔 tool window 방식으로 작업 표시줄과 Alt+Tab에서 숨기고 `Win + D` / 바탕화면 보기 이후에도 다시 바탕화면 레이어에 붙도록 유지한다.

Google 캘린더 일정을 iCal 주소로 읽어 보여 준다. 일정 추가와 수정은 Google 캘린더에서 한다.

## 구성

- `App.xaml`, `MainWindow.xaml`: 앱 진입점과 달력 화면
- `Models`: 설정, 날짜 칸, 일정, 캘린더 주소
- `Services`: 바탕화면 부착, 반투명 배경, iCal 피드, 공휴일 계산, 설정 저장

## 캘린더 연결

1. Google 캘린더 웹에서 설정 > 왼쪽의 캘린더 이름 > `캘린더 통합` > `iCal 형식의 비공개 주소`를 복사한다.
2. `%APPDATA%\DesktopCalendar\calendars.json`에 캘린더마다 한 줄씩 넣는다. 앱을 처음 실행하면 예시가 담긴 파일이 생긴다.

```json
[
  { "Url": "https://calendar.google.com/calendar/ical/.../basic.ics", "Color": "#8AB4F8" }
]
```

비공개 주소는 그 캘린더를 누구나 읽을 수 있게 하는 비밀번호와 같으므로 저장소나 다른 곳에 올리지 않는다. 앱은 5분마다, 그리고 우클릭 메뉴의 `새로 고침`으로 다시 읽는다.

## 공휴일

정기 공휴일과 대체공휴일은 앱이 직접 계산하고, 선거일·임시공휴일처럼 계산할 수 없는 날은 Google의 대한민국 공휴일 캘린더에서 보충한다. 두 곳이 겹치는 날은 앱의 계산을 따른다.

## 데이터 위치

- `%APPDATA%\DesktopCalendar\settings.json`: 창 위치와 크기 (앱이 관리)
- `%APPDATA%\DesktopCalendar\calendars.json`: 캘린더 iCal 주소와 색 (사용자가 편집)
- `%APPDATA%\DesktopCalendar\cache`: 마지막으로 받은 iCal 파일, 오프라인 시작용

## 사용

- 날짜 칸 더블클릭: 그 날짜를 브라우저의 Google 캘린더로 연다.
- 상단 바 드래그: 이동
- 창 가장자리/모서리 드래그: 크기 조절
- 우클릭 메뉴: `새로 고침`, `닫기`

## 개발 중 실행

저장소 최상위에서 실행한다.

```powershell
dotnet run
```

## 설치와 업데이트

실행 중인 달력을 우클릭 > `닫기`로 닫은 뒤, 저장소 최상위에서 배포한다. 업데이트도 같은 명령이다.

```powershell
dotnet publish -c Release -o "$env:LOCALAPPDATA\Programs\DesktopCalendar"
```

로그인할 때 자동으로 띄우려면 시작프로그램 폴더(`Win + R` > `shell:startup`)에 `%LOCALAPPDATA%\Programs\DesktopCalendar\DesktopCalendar.exe`의 바로 가기를 둔다. .NET 10 Desktop Runtime이 설치되어 있어야 한다.
