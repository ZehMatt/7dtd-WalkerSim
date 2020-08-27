mkdir dmt
mkdir dmt\Mods
mkdir dmt\Mods\WalkerSim
mkdir dmt\Mods\WalkerSim\Harmony
mkdir dmt\Mods\WalkerSim\Scripts

copy WalkerSim\*.cs dmt\Mods\WalkerSim\Scripts\*.cs
copy WalkerSim\Simulation\*.cs dmt\Mods\WalkerSim\Scripts\*.cs
copy ViewData\*.cs dmt\Mods\WalkerSim\Scripts\*.cs
copy WalkerSim\Harmony\*.cs dmt\Mods\WalkerSim\Harmony\*.cs
copy WalkerSim\ModInfo.xml dmt\Mods\WalkerSim\ModInfo.xml
copy WalkerSim\WalkerSim.xml dmt\Mods\WalkerSim\WalkerSim.xml
copy WalkerSim\mod.xml dmt\Mods\WalkerSim\mod.xml