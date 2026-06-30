using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using TL;
using WTelegram;

namespace ExportTelegramContacts
{
	class Program
	{
		private static Client _client;
		private static User _self;
		private static string _phoneNumber;

		public static int ApiId
		{
			get
			{
				var idStr = System.Configuration.ConfigurationManager.AppSettings["api_id"];
				int.TryParse(idStr, out var id);

				return id;
			}
		}
		public static string ApiHash => System.Configuration.ConfigurationManager.AppSettings["api_hash"] ?? "";

		static void Main(string[] args)
		{
			Console.WriteLine("***************************");
			Console.WriteLine($"Welcome to Telegram Contacts Exporter Version {Assembly.GetExecutingAssembly().GetName().Version}");
			Console.WriteLine("***************************");

			// WTelegramClient logs to console by default, only show warnings and above.
			Helpers.Log = (lvl, str) => { if (lvl >= 3) Console.WriteLine(str); };

			try
			{
				var apiId = ApiId;
				var apiHash = ApiHash;

				if (string.IsNullOrWhiteSpace(apiHash) ||
					apiHash.Contains("PLACEHOLDER") ||
					apiId <= 0)
				{
					Console.WriteLine("The values for 'api_id' or 'api_hash' are NOT provided. Please enter these value in the '.config' file and try again.");
					Console.ReadKey(intercept: true);
					return;
				}

				Console.Write("Connecting to Telegram servers...");
				_client = new Client(Config);
				var connect = _client.ConnectAsync();
				connect.Wait();
				Console.WriteLine("Connected");

				// If a previous session is already authorized, pick it up.
				if (_client.User != null)
					_self = _client.User;
			}
			catch (Exception ex)
			{
				Console.WriteLine();
				Console.WriteLine(ex.Message);
				Console.ReadKey(intercept: true);
				return;
			}

			char? WriteMenu()
			{
				if (!IsUserAuthorized())
				{
					Console.WriteLine("You are not authenticated, please authenticate first.");
				}

				Console.WriteLine();
				Console.WriteLine("***************************");
				Console.WriteLine("1: Authenticate");
				Console.WriteLine("2: Export Contacts");
				Console.WriteLine("Q: Quit");
				Console.WriteLine(" ");
				Console.Write("Please enter your choice: ");
				return Console.ReadLine()?.ToLower().FirstOrDefault();
			}

			while (true)
			{
				var userInput = WriteMenu();

				switch (userInput)
				{
					case 'q':
						_client?.Dispose();
						return;

					case '1':
						CallAuthenicate().Wait();
						break;

					case '2':
						CallExportContacts().Wait();
						break;

					default:
						Console.Clear();
						Console.WriteLine("Invalid input!");

						break;
				}
			}
		}

		private static bool IsUserAuthorized() => _client?.User != null;

		/// <summary>
		/// WTelegramClient configuration callback. It is called by the library whenever it
		/// needs a piece of configuration (api_id/api_hash/phone number/codes/session path...).
		/// </summary>
		private static string Config(string what)
		{
			switch (what)
			{
				case "api_id": return ApiId.ToString();
				case "api_hash": return ApiHash;
				case "phone_number": return _phoneNumber ?? PromptFor("Please enter your mobile number (e.g: 14155552671): ");
				case "verification_code": return PromptFor("Request is sent to your mobile or the telegram app associated with this number, please enter the code here: ");
				case "password": return PromptFor("Please enter your 2FA password: ", hidden: true);
				case "first_name": return "New"; // used only if registering a new account
				case "last_name": return "User"; // used only if registering a new account
				case "session_pathname": return "session.dat";
				default: return null; // let WTelegramClient use default behaviour for other config requests
			}
		}

		private static string PromptFor(string message, bool hidden = false)
		{
			Console.Write(message);
			if (!hidden)
				return Console.ReadLine();

			var pwd = new System.Text.StringBuilder();
			ConsoleKeyInfo key;
			while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
			{
				if (key.Key == ConsoleKey.Backspace && pwd.Length > 0)
					pwd.Remove(pwd.Length - 1, 1);
				else if (!char.IsControl(key.KeyChar))
					pwd.Append(key.KeyChar);
			}
			Console.WriteLine();
			return pwd.ToString();
		}

		private static async Task CallExportContacts()
		{
			try
			{
				if (!IsUserAuthorized())
				{
					Console.WriteLine("You are not authenticated, please authenticate first.");
					return;
				}

				Console.WriteLine($"Reading contacts...");

				var contacts = await _client.Contacts_GetContacts();

				var usersList = contacts.users.Values.OfType<User>().ToList();

				Console.WriteLine($"Number of contacts: {usersList.Count}");

				var fileName = $"ExportedContacts{Path.DirectorySeparatorChar}Exported-{DateTime.Now:yyyy-MM-dd HH-mm.ss}.vcf";
				var fileNameWihContacts = $"ExportedContacts{Path.DirectorySeparatorChar}Exported-WithPhoto-{DateTime.Now:yyyy-MM-dd HH-mm.ss}.vcf";

				Directory.CreateDirectory("ExportedContacts");

				Console.Write($"Export contacts without phone? [y/n] ");
				var filterResult = Console.ReadLine() ?? "";
				var dontExport = !(filterResult == "" || filterResult.ToLower() == "n");

				Console.WriteLine($"Writing to: {fileName}");
				using (var file = File.Create(fileName))
				using (var stringWrite = new StreamWriter(file))
				{
					var savedCount = 0;
					foreach (var user in usersList)
					{
						if (dontExport)
						{
							if (string.IsNullOrWhiteSpace(user.phone))
								continue;
						}

						//vCard Begin
						stringWrite.WriteLine("BEGIN:VCARD");
						stringWrite.WriteLine("VERSION:2.1");
						//Name
						stringWrite.WriteLine("N:" + user.last_name + ";" + user.first_name);
						//Full Name
						stringWrite.WriteLine("FN:" + user.first_name + " " +
											 /* nameMiddle + " " +*/ user.last_name);
						stringWrite.WriteLine("TEL;CELL:" + ConvertFromTelegramPhoneNumber(user.phone));

						//vCard End
						stringWrite.WriteLine("END:VCARD");

						savedCount++;
					}
					Console.WriteLine($"Total number of contacts saved: {savedCount}");
					Console.WriteLine();
				}

				Console.Write($"Do you want to export contacts with images? [y=enter/n] ");
				var exportWithImagesResult = Console.ReadLine() ?? "";
				var exportWithImages = exportWithImagesResult == "" || exportWithImagesResult.ToLower() == "y";

				if (exportWithImages)
				{
					Console.Write($"Save small or big images? [s=small=enter/b=big] ");
					var saveSmallResult = Console.ReadLine() ?? "";
					var saveSmallImages = saveSmallResult == "" || saveSmallResult.ToLower() == "s";

					Console.WriteLine($"Writing to: {fileNameWihContacts}");
					using (var file = File.Create(fileNameWihContacts))
					using (var stringWrite = new StreamWriter(file))
					{
						var savedCount = 0;
						foreach (var user in usersList)
						{
							if (dontExport)
							{
								if (string.IsNullOrWhiteSpace(user.phone))
									continue;
							}

							string userPhotoString = null;
							try
							{
								if (user.photo is UserProfilePhoto)
								{
									var displayName = user.first_name + " " + user.last_name;
									if (string.IsNullOrWhiteSpace(displayName))
										displayName = user.username;

									Console.Write($"Reading profile image for: {displayName}...");

									var photoBytes = await GetProfilePhoto(_client, user, big: !saveSmallImages);

									if (photoBytes != null && photoBytes.Length > 0)
									{
										// resize if it is the big image
										if (!saveSmallImages)
										{
											Console.Write("Resizing...");
											photoBytes = ResizeProfileImage(photoBytes);
										}

										userPhotoString = Convert.ToBase64String(photoBytes);

										Console.WriteLine("Done");
									}
									else
									{
										Console.WriteLine("No photo");
									}
								}
							}
							catch (Exception e)
							{
								Console.WriteLine("Failed due " + e.Message);
							}

							//vCard Begin
							stringWrite.WriteLine("BEGIN:VCARD");
							stringWrite.WriteLine("VERSION:2.1");
							//Name
							if (string.IsNullOrEmpty(user.last_name) && string.IsNullOrEmpty(user.first_name))
								stringWrite.WriteLine("N:" + user.username + ";");
							else
								stringWrite.WriteLine("N:" + user.last_name + ";" + user.first_name);
							//Full Name
							stringWrite.WriteLine("FN:" + user.first_name + " " +
												  /* nameMiddle + " " +*/ user.last_name);
							stringWrite.WriteLine("TEL;CELL:" + ConvertFromTelegramPhoneNumber(user.phone));

							if (userPhotoString != null)
							{
								stringWrite.WriteLine("PHOTO;ENCODING=BASE64;TYPE=JPEG:");
								stringWrite.WriteLine(userPhotoString);
								stringWrite.WriteLine(string.Empty);
							}

							//vCard End
							stringWrite.WriteLine("END:VCARD");

							savedCount++;
						}
						Console.WriteLine($"Total number of contacts with images saved: {savedCount}");
						Console.WriteLine();
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Unknown error, if the error conitinues removing 'session.dat' file may help.\r\n" + ex.Message);
				return;
			}
		}

		/// <summary>
		/// Downloads a user's profile photo using WTelegramClient's built-in helper,
		/// which takes care of locating the right datacenter and file location.
		/// </summary>
		private static async Task<byte[]> GetProfilePhoto(Client client, User user, bool big)
		{
			using (var mem = new MemoryStream())
			{
				var type = await client.DownloadProfilePhotoAsync(user, mem, big);
				if (type == Storage_FileType.unknown || mem.Length == 0)
					return null;

				return mem.ToArray();
			}
		}

		public static string ConvertFromTelegramPhoneNumber(string number)
		{
			if (string.IsNullOrEmpty(number))
				return number;
			if (number.StartsWith("0"))
				return number;
			if (number.StartsWith("+"))
				return number;
			return "+" + number;
		}

		private static async Task CallAuthenicate()
		{
			Console.Write("Please enter your mobile number (e.g: 14155552671): ");
			_phoneNumber = Console.ReadLine();

			const int maxAttempts = 5;
			for (var attempt = 1; attempt <= maxAttempts; attempt++)
			{
				try
				{
					// WTelegramClient drives the whole login flow (code, 2FA password, etc.)
					// through the Config callback; LoginUserIfNeeded() returns the logged in user.
					_self = await _client.LoginUserIfNeeded();

					Console.WriteLine($"Authenicaion was successfull for Person Name:{_self.first_name + " " + _self.last_name}, Username={_self.username}");
					break;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Attempt {attempt}/{maxAttempts} failed: {ex.Message}");
					if (attempt == maxAttempts)
					{
						Console.WriteLine("All attempts failed. Check your internet connection / try a VPN, or remove session.dat and retry.");
						Console.ReadKey(intercept: true);
						return;
					}
					await Task.Delay(2000 * attempt);
				}
			}
		}

		private static byte[] ResizeProfileImage(byte[] imageBytes)
		{
			const int vcardImageSize = 300;
			const int vcardImageQuality = 70;

			using (var imgMem = new MemoryStream(imageBytes))
			using (var img = SixLabors.ImageSharp.Image.Load(imgMem))
			{
				img.Mutate(x => x.Resize(new ResizeOptions
				{
					Mode = ResizeMode.Max,
					Size = new Size(vcardImageSize, vcardImageSize)
				}));

				using (var mediumImageStream = new MemoryStream())
				{
					var encoder = new JpegEncoder { Quality = vcardImageQuality };
					img.Save(mediumImageStream, encoder);

					// the new image should be smaller than the original one
					if (mediumImageStream.Length > imageBytes.Length)
						return imageBytes;

					return mediumImageStream.ToArray();
				}
			}
		}
	}
}
