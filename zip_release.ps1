$ver = Read-Host -Prompt 'Enter the version'
mkdir release -Force > $null
cd dmt
Compress-Archive .\* ..\release\WalkerSim.$ver-DMT.zip -Force
cd ..
cd bin
Compress-Archive .\* ..\release\WalkerSim.$ver-Dedicated.zip -Force
cd ..