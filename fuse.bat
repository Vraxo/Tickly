@echo off
REM Runs the Fuse tool for the Cosmocrush project

echo Starting Fuse process for Cosmocrush...
echo Outputting to: D:\Parsa Stuff\Godot\Cosmocrush\MergedCodeForAI.txt
echo Scanning folders:
echo   - D:\Parsa Stuff\Visual Studio\Tickly\Tickly\Source
echo ==================================================

Fuse.exe "D:\Parsa Stuff\Visual Studio\Tickly\MergedCodeForAI.txt" "D:\Parsa Stuff\Visual Studio\Tickly\Tickly"

echo ==================================================
echo Fuse process finished. Press any key to close this window.
pause