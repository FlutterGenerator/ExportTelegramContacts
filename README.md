# No longer maintained
There are alternative apps in Mobile App Stores, this repo is no longer maintained, if you think I should countinue this for a good reason you can contact me, thanks.

# Migration note
This fork has been migrated from the abandoned **TLSharp** library to **[WTelegramClient](https://wiz0u.github.io/WTelegramClient/)**, and the project was converted from the old .NET Framework 4.7 / `packages.config` format to a modern SDK-style **.NET 8** project. Image resizing now uses `SixLabors.ImageSharp` instead of `System.Drawing` for cross-platform support. Usage (config keys, menu, vCard export) is unchanged.

Build/run with the .NET SDK:
```
dotnet build
dotnet run --project ExportTelegramContacts/ExportTelegramContacts.csproj
```

# ExportTelegramContacts
Easily export Telegram contacts with profile images in **vCard** format

# Executable Binary
You can download the latest executable binary from here:
	https://github.com/salarcode/ExportTelegramContacts/releases


# How to use
1- First you have to register at https://my.telegram.org/auth

2- Then you have to register an application for your phone at https://my.telegram.org/apps

3- Enter the retrieved 'api_id' and 'api_hash' in **ExportTelegramContacts.exe.config** or **app.config** file.

4- Run the application and choose option '1' and authenticate as instructed.
NOTE: If you have Telegram application associated with the mobile number you've entered, you will most likely receive the code in the app instead of a SMS.

5- You are good to export the contacts

![MainMenu](https://github.com/salarcode/ExportTelegramContacts/blob/master/ExportTelegramContacts/Screenshots/MainMenu.png)
