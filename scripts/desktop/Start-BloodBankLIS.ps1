# Starts the Greyhound Blood Bank LIS API and Web UI, then opens the browser.
# The API stays in the background only while the UI is running; closing the
# browser, choosing Exit, or closing this window stops both processes.
$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$env:DesktopHost__ShutdownOnExit = "true"

# Stop prior API/Web instances so a rebuild is picked up and ports are free.
Get-Process BloodBankLIS.Api, BloodBankLIS.Web -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

$apiProject = Join-Path $Root "src\BloodBankLIS.Api\BloodBankLIS.Api.csproj"
$webProject = Join-Path $Root "src\BloodBankLIS.Web\BloodBankLIS.Web.csproj"
$apiUrl = "http://localhost:5177"
$webUrl = "http://localhost:5291"
$logDir = Join-Path $env:LOCALAPPDATA "BloodBankLIS\logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$apiOut = Join-Path $logDir "api-stdout.log"
$apiErr = Join-Path $logDir "api-stderr.log"
$webOut = Join-Path $logDir "web-stdout.log"
$webErr = Join-Path $logDir "web-stderr.log"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "The .NET SDK was not found. Install .NET 10 SDK and try again." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

function Wait-HttpReady {
    param(
        [string]$Url,
        [string]$Name,
        [int]$TimeoutSeconds = 120,
        [string]$StdErrLog = $null,
        [System.Diagnostics.Process]$Process = $null
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastProgress = Get-Date
    while ((Get-Date) -lt $deadline) {
        if ($null -ne $Process -and $Process.HasExited) {
            Write-Host "$Name process exited early (exit code $($Process.ExitCode))." -ForegroundColor Red
            return $false
        }

        if ($StdErrLog -and (Test-Path $StdErrLog)) {
            $fatal = Select-String -Path $StdErrLog -Pattern "Unhandled exception|Database migrate/seed failed|fail: Program" -SimpleMatch:$false -ErrorAction SilentlyContinue |
                Select-Object -First 1
            if ($fatal) {
                Write-Host "$Name reported a startup failure in logs." -ForegroundColor Red
                return $false
            }
        }

        try {
            # Do not follow redirects: Development HTTP profiles often emit an HTTPS
            # redirect that has no listener, which would look like a failed startup.
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2 -MaximumRedirection 0
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                Write-Host "$Name is ready at $Url" -ForegroundColor Green
                return $true
            }
        }
        catch {
            $statusCode = $null
            try { $statusCode = [int]$_.Exception.Response.StatusCode } catch { }
            if ($statusCode -ge 200 -and $statusCode -lt 500) {
                Write-Host "$Name is ready at $Url" -ForegroundColor Green
                return $true
            }
        }

        if (((Get-Date) - $lastProgress).TotalSeconds -ge 10) {
            Write-Host "  Waiting for $Name..." -ForegroundColor DarkGray
            $lastProgress = Get-Date
        }

        Start-Sleep -Seconds 2
    }

    Write-Host "$Name did not become ready within $TimeoutSeconds seconds." -ForegroundColor Red
    return $false
}

function Show-LogTail {
    param([string[]]$Paths)
    foreach ($path in $Paths) {
        if (Test-Path $path) {
            Write-Host "---- $path ----" -ForegroundColor Yellow
            Get-Content $path -Tail 30
        }
    }
}

function Stop-LisProcesses {
    param([System.Diagnostics.Process[]]$Started = @())

    Get-Process BloodBankLIS.Api, BloodBankLIS.Web -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    foreach ($proc in $Started) {
        if ($null -ne $proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            Get-CimInstance Win32_Process -Filter "ParentProcessId=$($proc.Id)" -ErrorAction SilentlyContinue |
                ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        }
    }
}

function New-KillOnCloseJob {
    if (-not ("BloodBankLIS.WinJob" -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
namespace BloodBankLIS {
    public static class WinJob {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        public const int JobObjectExtendedLimitInformation = 9;
        public const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        public static IntPtr CreateKillOnCloseJob() {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr ptr = Marshal.AllocHGlobal(length);
            try {
                Marshal.StructureToPtr(info, ptr, false);
                if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ptr, (uint)length))
                    throw new System.ComponentModel.Win32Exception();
            } finally {
                Marshal.FreeHGlobal(ptr);
            }
            return job;
        }
    }
}
"@
    }

    [BloodBankLIS.WinJob]::CreateKillOnCloseJob()
}

function Add-ProcessToJob {
    param([IntPtr]$Job, [System.Diagnostics.Process]$Process)
    if ($null -eq $Process -or $Process.HasExited -or $Job -eq [IntPtr]::Zero) {
        return
    }

    if (-not ("BloodBankLIS.WinJob" -as [type])) {
        return
    }

    try {
        [void][BloodBankLIS.WinJob]::AssignProcessToJobObject($Job, $Process.Handle)
    }
    catch {
        # Nested jobs or breakaway children are non-fatal; Exit/browser close still stops the API.
    }
}

Write-Host "Starting Greyhound Blood Bank LIS..."
Write-Host "  Logs: $logDir"

# Clear prior redirected logs so crash detection is for this run only.
Remove-Item $apiOut, $apiErr, $webOut, $webErr -ErrorAction SilentlyContinue

$apiProc = $null
$webProc = $null
$job = [IntPtr]::Zero
$exitCode = 0

try {
    $job = New-KillOnCloseJob
}
catch {
    Write-Host "Could not create a Windows job object; close the browser or use Exit so the API stops." -ForegroundColor DarkYellow
}

try {
    # API first (Web depends on it)
    $apiProc = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $apiProject, "--launch-profile", "http") `
        -WorkingDirectory $Root `
        -RedirectStandardOutput $apiOut `
        -RedirectStandardError $apiErr `
        -WindowStyle Hidden `
        -PassThru
    Add-ProcessToJob -Job $job -Process $apiProc

    if (-not (Wait-HttpReady -Url $apiUrl -Name "API" -TimeoutSeconds 180 -StdErrLog $apiErr -Process $apiProc)) {
        Write-Host ""
        Write-Host "API failed to start. Last log lines:" -ForegroundColor Red
        Show-LogTail -Paths @($apiOut, $apiErr)
        $exitCode = 1
    }
    else {
        Get-Process BloodBankLIS.Api -ErrorAction SilentlyContinue | ForEach-Object {
            Add-ProcessToJob -Job $job -Process $_
        }

        $webProc = Start-Process -FilePath "dotnet" `
            -ArgumentList @("run", "--project", $webProject, "--launch-profile", "http") `
            -WorkingDirectory $Root `
            -RedirectStandardOutput $webOut `
            -RedirectStandardError $webErr `
            -WindowStyle Hidden `
            -PassThru
        Add-ProcessToJob -Job $job -Process $webProc

        if (-not (Wait-HttpReady -Url $webUrl -Name "Web UI" -TimeoutSeconds 180 -StdErrLog $webErr -Process $webProc)) {
            Write-Host ""
            Write-Host "Web UI failed to start. Last log lines:" -ForegroundColor Red
            Show-LogTail -Paths @($webOut, $webErr)
            $exitCode = 1
        }
        else {
            Get-Process BloodBankLIS.Web -ErrorAction SilentlyContinue | ForEach-Object {
                Add-ProcessToJob -Job $job -Process $_
            }

            Start-Process $webUrl
            Write-Host "Greyhound Blood Bank LIS is running at $webUrl" -ForegroundColor Green
            Write-Host "Close the browser, choose Exit, or close this window to stop the API." -ForegroundColor DarkGray

            if (-not $webProc.HasExited) {
                Wait-Process -Id $webProc.Id
            }
        }
    }
}
finally {
    Write-Host "Stopping the API and Web UI..."
    Stop-LisProcesses -Started @($apiProc, $webProc)
}

if ($exitCode -ne 0) {
    Read-Host "Press Enter to close"
    exit $exitCode
}
