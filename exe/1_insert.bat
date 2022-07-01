del error.txt
del /q ins\*
copy orig\* ins

echo trans\0005DFBC.txt >> error.txt
tools\atlas ins\Bunny3.exe trans\0005DFBC.txt >> error.txt