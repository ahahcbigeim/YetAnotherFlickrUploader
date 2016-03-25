using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YetAnotherFlickrUploader.Helpers;
using YetAnotherFlickrUploader.Services;
using FlickrNet;

namespace YetAnotherFlickrUploader
{
	class Program
	{
		static readonly int BatchSizeForParallelUpload = Convert.ToInt32(ConfigurationManager.AppSettings["BatchSizeForParallelUpload"]);
		static readonly int BatchSizeForParallelProcessing = Convert.ToInt32(ConfigurationManager.AppSettings["BatchSizeForParallelProcessing"]);

		static ModesEnum _mode;

		static void Main(string[] args)
		{
			#region Parse args

			string path = null;
			string modeSwitch = null;

			if (args != null)
			{
				int i = 0;
				while (i < args.Length)
				{
					string arg = args[i++];
					if (arg.StartsWith("--"))
					{
						modeSwitch = arg;
					}
					else
					{
						path = arg;
					}
				}
			}

			if (string.IsNullOrEmpty(path))
			{
				path = Environment.CurrentDirectory;
			}

			_mode = Options.GetModeFromArgs(modeSwitch);

			#endregion

			#region Authenticate with Flickr API

			Logger.Debug("Authenticating...");

			var token = Authenticate();

			if (token == null)
			{
				Logger.Error("Could not authenticate.");
				return;
			}

			Logger.Info("Authenticated as " + token.FullName + ".");

			Uploader.UserId = token.UserId;
			Uploader.Flickr = FlickrManager.GetAuthInstance();

			#endregion

			try
			{
				ProcessDirectory(path);
			}
			catch (Exception e)
			{
				Console.WriteLine();
				Logger.Error("Upload failed.", e);
			}
		}

		static OAuthAccessToken Authenticate()
		{
			OAuthAccessToken token = FlickrManager.OAuthToken;

			if (token == null || token.Token == null)
			{
				ConsoleHelper.WriteInfoLine("Requesting access token...");

				Flickr flickr = FlickrManager.GetInstance();
				OAuthRequestToken requestToken = flickr.OAuthGetRequestToken("oob");

				string url = flickr.OAuthCalculateAuthorizationUrl(requestToken.Token, AuthLevel.Write);

				Process.Start(url);

				ConsoleHelper.WriteInfo("Verifier: ");
				string verifier = Console.ReadLine();

				token = flickr.OAuthGetAccessToken(requestToken, verifier);
				FlickrManager.OAuthToken = token;
			}

			return token;
		}

		static void ProcessDirectory(string path)
		{
			if (!Directory.Exists(path))
			{
				Logger.Error("The specified path is invalid.");
				return;
			}

			var files = FindPictureFiles(path);

			if (!files.Any())
			{
				Logger.Warning("Could not locate any files to upload in the directory: {0}.", path);

				if (_mode == ModesEnum.Upload)
				{
					return;
				}
			}
			else
			{
				Logger.Debug("Processing files in the directory: {0}.", path);
			}

			string photosetName = Path.GetFileName(path.TrimEnd('\\')); //GetPhotosetTitle(files.First());

			var photoset = Uploader.FindPhotosetByName(photosetName);
			bool photosetExists = photoset != null && photoset.Title == photosetName;
			bool photosetChanged = false;
			string photosetId = null;

			List<Photo> photosetPhotos = photosetExists
				? Uploader.GetPhotosetPictures(photoset.PhotosetId)
				: new List<Photo>();

			if (photosetExists)
			{
				photosetId = photoset.PhotosetId;

				var totalFilesInDirectory = files.Count;
				files.RemoveAll(x => photosetPhotos.Any(p => p.Title == GetPhotoTitle(x)));
				Logger.Info("{0} out of {1} files are already in the existing photoset.", totalFilesInDirectory - files.Count, totalFilesInDirectory);
			}

			// Check again as the collection might have been modified
			if (!files.Any())
			{
				Logger.Warning("All photos are already in the photoset. Nothing to upload.");
			}
			else
			{
				if (ConsoleHelper.ConfirmYesNo(string.Format("Agree to upload {0} files to {1} photoset '{2}'?", files.Count, photosetExists ? "the existing" : "a new", photosetName)))
				{
					photosetChanged = true;

					#region Upload photos

					var photoIds = new List<string>();

					Console.WriteLine();
					Logger.Info("Uploading files...");

					var failures = ParallelExecute(files, fileName =>
					{
						var title = GetPhotoTitle(fileName);
						// Check if picture is not in the photoset (if it exists)
						var photo = photosetPhotos.FirstOrDefault(x => x.Title == title); //?? Uploader.FindPictureByName(title);
						if (photo == null || photo.Title != title)
						{
							// No such picture found - uploading
							var photoId = Uploader.UploadPicture(fileName, title, null, null);
							if (photoIds.Contains(photoId))
							{
								//uploaded twice?
								throw new Exception(string.Format("{0} is already in the list of uploaded files.", title));
							}
							photoIds.Add(photoId);
						}
					}, BatchSizeForParallelUpload);

					if (failures.Any())
					{
						Logger.Error("Uploaded with errors:");
						foreach (var failure in failures)
						{
							Logger.Error("{0,-20}: {1}", failure.Key, failure.Value);
						}
					}
					else
					{
						Logger.Info("All files were successfully uploaded.");
					}

					#endregion

					if (!photoIds.Any())
					{
						Logger.Warning("No files were uploaded to '{0}'.", photosetName);
					}
					else if (!photosetExists)
					{
						#region Create new photoset

						Console.WriteLine();
						Logger.Info("Creating photoset '{0}'...", photosetName);

						// Set the first photo in the set as its cover
						var coverPhotoId = photoIds.First();
						photoIds.Remove(coverPhotoId);

						// Create new photoset
						photoset = Uploader.CreatePhotoSet(photosetName, coverPhotoId);
						photosetId = photoset.PhotosetId;

						Logger.Info("Photoset created.");

						photosetExists = true;

						#endregion

						#region Move photos to the photoset

						Console.WriteLine();
						Logger.Info("Moving uploaded files to the photoset...");

						var fails = ParallelExecute(photoIds, id => Uploader.AddPictureToPhotoSet(id, photosetId),
							BatchSizeForParallelProcessing);

						if (!fails.Any())
						{
							Logger.Info("Uploaded pictures were successfully moved to '{0}'.", photosetName);
						}
						else
						{
							Logger.Error("Moved with errors:");
							foreach (var fail in fails)
							{
								Logger.Error("{0,-20}: {1}", fail.Key, fail.Value);
							}
						}

						#endregion
					}
				}
			}

			bool updatePermissions = _mode == ModesEnum.ShareWithFamily || _mode == ModesEnum.ShareWithFriends;

			if (photosetExists && (photosetChanged || updatePermissions))
			{
				// Get all photos in the photoset
				photosetPhotos = Uploader.GetPhotosetPictures(photosetId);

				#region Validate photos in the photoset

				ValidateDirectory(path, photosetPhotos);

				#endregion

				if (photosetPhotos.Count > 1)
				{
					#region Sort photos in the photoset

					if (photosetChanged)
					{
						SortPhotosInSet(photosetPhotos);
					}

					#endregion

					#region Set permissions

					if (updatePermissions)
					{
						SetPermissions(photosetPhotos, _mode == ModesEnum.ShareWithFamily, _mode == ModesEnum.ShareWithFriends);
					}

					#endregion
				}
			}
		}

		private static List<string> FindPictureFiles(string directory)
		{
			return Directory.EnumerateFiles(directory)
				.Where(file => file.ToLower().EndsWith("jpg") || file.ToLower().EndsWith("jpeg"))
				.ToList();
		}

		private static void ValidateDirectory(string directory, IEnumerable<Photo> photosetPhotos)
		{
			// Rescan the directory
			List<string> files = FindPictureFiles(directory);

			List<string> photosetPhotoTitles = photosetPhotos.Select(p => p.Title).ToList();

			// Find files which were not uploaded to the photoset
			var leftFiles = files.Select(GetPhotoTitle).Where(x => !photosetPhotoTitles.Contains(x)).ToList();
			if (leftFiles.Any())
			{
				Console.WriteLine();
				Logger.Warning("Some files were not uploaded:");
				foreach (var leftFile in leftFiles)
				{
					ConsoleHelper.WriteWarningLine(leftFile);
				}
			}

			// Find duplicates in the photoset
			var duplicates = photosetPhotoTitles
				.GroupBy(x => x)
				.Select(g => new { Title = g.Key, Count = g.Count() })
				.Where(x => x.Count > 1)
				.ToList();
			if (duplicates.Any())
			{
				Console.WriteLine();
				Logger.Warning("Some files have duplicates:");
				foreach (var duplicate in duplicates)
				{
					Logger.Warning("{0,-20} x{1}", duplicate.Title, duplicate.Count);
				}
			}
		}

		private static void SortPhotosInSet(List<Photo> photosetPhotos)
		{
			List<Photo> orderedList = photosetPhotos.OrderBy(x => x.DateTaken).ToList();
			DateTime maxDateUploaded = orderedList.Select(x => x.DateUploaded).Last();
			int number = orderedList.Count;

			Console.WriteLine();
			Logger.Info("Setting photo upload dates in the photoset...");

			var fails = ParallelExecute(orderedList, photo =>
			{
				DateTime dateUploaded = maxDateUploaded.AddSeconds(-1 * number--);
				Uploader.SetPhotoUploadDate(photo.PhotoId, dateUploaded);

				RestoreCursorPosition();
				ConsoleHelper.WriteDebug("{0} of {1}.", number, photosetPhotos.Count);
			}, BatchSizeForParallelProcessing);

			if (!fails.Any())
			{
				Logger.Info("Successfully processed all photos in the photoset.");
			}
			else
			{
				Logger.Error("Processed with errors:");
				foreach (var fail in fails)
				{
					Logger.Error("{0,-20}: {1}", fail.Key, fail.Value);
				}
			}
		}

		private static void SetPermissions(List<Photo> photosetPhotos, bool family, bool friends)
		{
			Console.WriteLine();
			Logger.Info("Setting permissions in the photoset...");

			var fails = ParallelExecute(photosetPhotos,
				photo => Uploader.SetPermissions(photo.PhotoId, false, friends, family),
				BatchSizeForParallelProcessing);

			if (!fails.Any())
			{
				Logger.Info("Successfully processed all photos in the photoset.");
			}
			else
			{
				Logger.Error("Processed with errors:");
				foreach (var fail in fails)
				{
					Logger.Error("{0,-20}: {1}", fail.Key, fail.Value);
				}
			}
		}

		private static string GetPhotosetTitle(string path)
		{
			return Path.GetFileName(Path.GetDirectoryName(path));
		}

		private static string GetPhotoTitle(string path)
		{
			string photosetName = GetPhotosetTitle(path);
			string fileName = Path.GetFileNameWithoutExtension(path);
			return string.Format("{0} - {1}", photosetName, fileName);
		}

		private static Dictionary<T, string> ParallelExecute<T>(List<T> source, Action<T> action, int batchSize)
		{
			var failures = new ConcurrentDictionary<T, string>();

			ConsoleHelper.WriteDebug("Progress: ");
			SaveCursorPosition();

			int processed = 0,
				total = source.Count;

			ConsoleHelper.WriteDebug("{0} of {1}.", processed, total);

			var locker = new object();

			DateTime start = DateTime.Now;

			while (source.Any())
			{
				// Take no more than {batchSize} files for parallel processing
				var batch = source.Take(batchSize).ToList();

				var tasks = batch
					.Select(item =>
						Task.Factory.StartNew(() =>
						{
							try
							{
								action.Invoke(item);
							}
							catch (Exception e)
							{
								failures.TryAdd(item, e.Message);
							}

							source.Remove(item);

							lock (locker)
							{
								processed += 1;

								DateTime now = DateTime.Now;

								TimeSpan elapsed = now - start;
								long timePerItem = elapsed.Ticks / processed;
								var eta = new TimeSpan((total - processed) * timePerItem);

								RestoreCursorPosition();
								ConsoleHelper.WriteDebug("{0} of {1}. Est. time: {2}. Elapsed: {3}.", processed, total, TimeSpanToReadableString(eta), TimeSpanToReadableString(elapsed));
								ConsoleHelper.WriteDebug("{0,20}", " ");
							}
						}, TaskCreationOptions.LongRunning))
					.ToArray();

				// Wait for batch to be processed
				Task.WaitAll(tasks);

				// Pause for 1 sec after each batch
				//Thread.Sleep(1000);
			}

			SetCursorPosition(0);
			Logger.Debug("Done in {0}.{1,60}", TimeSpanToReadableString(DateTime.Now - start), " ");

			return new Dictionary<T, string>(failures);
		}

		private static string TimeSpanToReadableString(TimeSpan span)
		{
			//return new DateTime(span.Ticks).ToString("hh:mm:ss");

			string formatted;
			if (span.TotalDays > 1)
			{
				formatted = string.Format("{0}{1}",
					string.Format("{0:0} day{1}, ", span.Days, span.Days == 1 ? String.Empty : "s"),
					span.TotalHours > 0 ? string.Format("{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? String.Empty : "s") : string.Empty);
			}
			else if (span.TotalHours > 1)
			{
				formatted = string.Format("{0}{1}",
					string.Format("{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? String.Empty : "s"),
					span.TotalMinutes > 0 ? string.Format("{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? String.Empty : "s") : string.Empty);
			}
			else if (span.TotalMinutes > 1)
			{
				formatted = string.Format("{0}{1}",
					string.Format("{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? String.Empty : "s"),
					span.TotalSeconds > 0 ? string.Format("{0:0} second{1}", span.Seconds, span.Seconds == 1 ? String.Empty : "s") : string.Empty);
			}
			else if (span.TotalSeconds > 1)
			{
				formatted = string.Format("{0:0} second{1}", span.Seconds, span.Seconds == 1 ? String.Empty : "s");
			}
			else
			{
				formatted = "0 seconds";
			}

			if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

			return formatted;

			/*
			string formatted = string.Format("{0}{1}{2}{3}",
				span.TotalDays > 0 ? string.Format("{0:0} day{1}, ", span.Days, span.Days == 1 ? String.Empty : "s") : string.Empty,
				span.TotalHours > 0 ? string.Format("{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? String.Empty : "s") : string.Empty,
				span.TotalMinutes > 0 ? string.Format("{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? String.Empty : "s") : string.Empty,
				span.TotalSeconds > 0 ? string.Format("{0:0} second{1}", span.Seconds, span.Seconds == 1 ? String.Empty : "s") : string.Empty);

			if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

			if (string.IsNullOrEmpty(formatted)) formatted = "0 seconds";

			return formatted;
			*/
		}

		#region Console helpers

		private static int? _cursorPosY;
		private static int? _cursorPosX;

		public static void SaveCursorPosition()
		{
			_cursorPosY = Console.CursorTop;
			_cursorPosX = Console.CursorLeft;
		}

		public static void RestoreCursorPosition()
		{
			SetCursorPosition(_cursorPosX, _cursorPosY);
		}

		public static void SetCursorPosition(int? left, int? top = null)
		{
			if (top.HasValue)
				Console.CursorTop = top.Value;
			if (left.HasValue)
				Console.CursorLeft = left.Value;
		}

		#endregion
	}
}
