param(
  [string]$Api = "http://localhost:5173",
  [string]$Email = "demo@example.com",
  [string]$Password = "Demo@12345"
)

$ErrorActionPreference = "Stop"
$script:PASS = 0
$script:FAIL = 0
$script:failures = @()
$script:h = $null

function Write-Intent {
  param([string]$Text, [string]$Expected, [string]$Case, [string]$SessionId, [string]$Language = "Auto")
  try {
    $body = @{ text = $Text; language = $Language; sessionId = $SessionId; isVoice = $false } | ConvertTo-Json -Compress
    $r = Invoke-RestMethod -Uri "$Api/api/assistant/command" -Method Post -Headers $script:h -ContentType "application/json; charset=utf-8" -Body $body -TimeoutSec 30
    if ($r.data.intent -eq $Expected) {
      $script:PASS++
      Write-Host "PASS [$Case] $($r.data.intent) <- $Text" -ForegroundColor Green
    } else {
      $script:FAIL++
      $script:failures += "INTENT mismatch [$Case]: want=$Expected got=$($r.data.intent) text='$Text'"
      Write-Host "FAIL [$Case] want=$Expected got=$($r.data.intent) text='$Text'" -ForegroundColor Red
    }
  } catch {
    $script:FAIL++
    $script:failures += "EXCEPTION [$Case]: text='$Text' err=$($_.Exception.Message)"
    Write-Host "FAIL [$Case] EXCEPTION text='$Text' <- $($_.Exception.Message)" -ForegroundColor Red
  }
}

Write-Host "== Login ==" -ForegroundColor Cyan
$login = Invoke-RestMethod -Uri "$Api/api/auth/login" -Method Post -ContentType "application/json" -Body (@{ email = $Email; password = $Password } | ConvertTo-Json -Compress)
$script:h = @{ Authorization = "Bearer $($login.data.accessToken)" }
Write-Host "OK." -ForegroundColor Green

Write-Host "== EN intents ==" -ForegroundColor Cyan
$en = @(
  @("Hello", "Greeting"),
  @("Good morning", "Greeting"),
  @("Thanks", "Greeting"),
  @("Help", "Help"),
  @("Create a task called Buy milk", "CreateTask"),
  @("Show my tasks", "ListTasks"),
  @("List my reminders", "ListReminders"),
  @("List my notes", "ListNotes"),
  @("List my appointments", "ListAppointments"),
  @("What is my schedule today?", "TodaySchedule"),
  @("What is my schedule tomorrow?", "TomorrowSchedule"),
  @("Take a note about the meeting", "CreateNote"),
  @("Create a reminder at 5pm to wash plants", "CreateReminder"),
  @("Schedule a meeting with Ravi tomorrow at 10am", "CreateAppointment"),
  @("Search for groceries", "SearchNotes"),
  @("Cancel", "CancelAction")
)
foreach ($c in $en) { Write-Intent $c[0] $c[1] "en" $c[0] }

Write-Host "== Multi-turn confirmation ==" -ForegroundColor Cyan
$sid = [guid]::NewGuid().ToString()
Write-Intent "Schedule a meeting with Priya tomorrow at 9:30am" "CreateAppointment" "mt-turn1" $sid
Write-Intent "Yes" "CreateAppointment" "mt-turn2" $sid
$sid2 = [guid]::NewGuid().ToString()
Write-Intent "Complete task Review deployment" "CompleteTask" "mt-c1" $sid2
Write-Intent "No" "CancelAction" "mt-c2" $sid2

Write-Host "== Language switch round-trip ==" -ForegroundColor Cyan
$sidL = [guid]::NewGuid().ToString()
Write-Intent "switch to Hindi" "ChangeLanguage" "lang-hi" $sidL
$now = (Invoke-RestMethod -Uri "$Api/api/settings" -Headers $h).data.language
if ($now -eq "hi") {
  $script:PASS++
  Write-Host "PASS [lang-check] settings switched to hi" -ForegroundColor Green
} else {
  $script:FAIL++
  $script:failures += "settings.language expected hi got $now"
  Write-Host "FAIL [lang-check] settings expected hi got $now" -ForegroundColor Red
}
Write-Intent "switch to English" "ChangeLanguage" "lang-en" $sidL

Write-Host ""
Write-Host "==========================================" -ForegroundColor DarkGray
Write-Host "RESULT: $($script:PASS) passed, $($script:FAIL) failed" -ForegroundColor $(if ($script:FAIL -eq 0) { "Green" } else { "Red" })
if ($script:failures.Count -gt 0) {
  Write-Host "`nFailures:" -ForegroundColor Yellow
  $script:failures | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
}
exit ([int]($script:FAIL -gt 0))