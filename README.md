# Desktop Calendar

Windows 바탕화면에 붙어 있는 반투명 월간 달력이다. Google 캘린더 일정과 한국 공휴일을 보여 준다. 작업 표시줄과 Alt+Tab에는 나타나지 않고, `Win + D`로 바탕화면을 본 뒤에도 그대로 남아 있다.

일정은 보기만 한다. 추가와 수정은 Google 캘린더에서 한다.

## 사용

- 상단 바: 보고 있는 연월과 이전 달·다음 달 버튼. 일정을 받아 오지 못하면 `갱신 실패`가 뜬다.
- 날짜 칸: 일정이 캘린더 색으로 표시된다. 칸에 다 들어가지 않으면 `+N`으로 줄인다.
- 날짜 칸 더블클릭: 그 날짜를 브라우저의 Google 캘린더로 연다.
- 상단 바 드래그: 이동
- 창 가장자리·모서리 드래그: 크기 조절
- 우클릭 메뉴: `새로 고침`, `닫기`

일정은 5분마다 새로 받아 온다. 인터넷이 끊겨도 마지막으로 받은 일정을 보여 준다.

## 캘린더 연결

1. Google 캘린더 웹에서 설정 > 왼쪽의 캘린더 이름 > `캘린더 통합` > `iCal 형식의 비공개 주소`를 복사한다.
2. 저장소 폴더에서 입력 도구를 실행하고, 안내에 따라 주소와 색을 붙여 넣는다. 캘린더가 여러 개면 반복한다.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\add-calendar.ps1
```

3. 달력에서 우클릭 > `새로 고침`을 누른다.

비공개 주소는 그 캘린더를 누구나 읽을 수 있게 하는 비밀번호와 같다. 다른 곳에 붙여 넣거나 공유하지 않는다. 새어 나간 것 같으면 같은 설정 화면에서 비공개 주소를 재설정하고, 새 주소로 다시 등록한다.

## 공휴일

설날·추석·대체공휴일 같은 정기 공휴일은 앱이 직접 계산한다. 선거일이나 임시공휴일처럼 미리 알 수 없는 날은 Google의 대한민국 공휴일 캘린더에서 가져온다.

## 설치와 업데이트

.NET 10 Desktop Runtime이 필요하다. 실행 중인 달력을 우클릭 > `닫기`로 닫은 뒤, 저장소 폴더에서 실행한다. 업데이트도 같은 명령이다.

```powershell
dotnet publish -c Release -o "$env:LOCALAPPDATA\Programs\DesktopCalendar"
```

로그인할 때 자동으로 띄우려면 `Win + R` > `shell:startup`으로 시작프로그램 폴더를 열고, `%LOCALAPPDATA%\Programs\DesktopCalendar\DesktopCalendar.exe`의 바로 가기를 넣는다.

## 데이터 위치

모두 `%APPDATA%\DesktopCalendar` 안에 있다.

- `settings.json`: 창 위치와 크기
- `calendars.json`: 등록한 캘린더 주소와 색
- `cache`: 마지막으로 받은 일정
