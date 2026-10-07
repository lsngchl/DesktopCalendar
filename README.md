# Desktop Calendar

Windows 바탕화면에 붙는 독립형 WPF 달력 앱이다. Rainmeter 없이 실행되며, `Progman`을 owner로 둔 tool window 방식으로 작업 표시줄과 Alt+Tab에서 숨기고 `Win + D` / 바탕화면 보기 이후에도 다시 바탕화면 레이어에 붙도록 유지한다.

## 구성

- `App.xaml`, `MainWindow.xaml`: 앱 진입점과 달력 화면
- `Models`: 설정, 날짜 칸
- `Services`: 바탕화면 부착, 반투명 배경, 공휴일 계산, 설정 저장

## 데이터 위치

- `%APPDATA%\DesktopCalendar\settings.json`: 앱 설정

## 기능

- DWM acrylic blur 기반의 반투명 월간 달력
- 오늘 날짜 테두리 표시
- 한국 공휴일 표시
- 우클릭 메뉴의 `닫기`로 종료
- 상단 바 드래그 이동
- WPF 창 가장자리/모서리 드래그 크기 조절

## 실행

저장소 최상위에서 실행한다.

```powershell
dotnet run
```
