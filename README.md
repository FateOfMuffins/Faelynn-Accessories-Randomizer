# Faelynn Accessories Randomizer

There are 28 outfit combinations, including the default. The overlay shows one each time you launch GF2. Click it to close.

<img src="GF2-Accessories.png" width="280" alt="Faelynn accessory options">

## Install

Download the [Windows setup app](https://github.com/FateOfMuffins/Faelynn-Accessories-Randomizer/releases/latest/download/Faelynn-Accessories-Randomizer-Setup.exe) and click **Install**.

The default delay is **30 seconds**. To change it, open **Faelynn Accessories Randomizer** from the Start menu, enter a new time, then press Enter or leave the field.

With **Start at sign-in** off, open the app and click **Start** before launching GF2.

## Build

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

The installer goes in `dist`.
