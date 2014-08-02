using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YetAnotherFlickrUploader.Helpers;
using YetAnotherFlickrUploader.Services;
using FlickrNet;

namespace YetAnotherFlickrUploader
{
	class Program
	{
		private const int BatchSizeForParallelUpload = 20;

		static void Main(string[] args)
		{
			var token = Authenticate();

			if (token == null)
			{
				ConsoleHelper.WriteErrorLine("Could not authenticate.");
				return;
			}

			ConsoleHelper.WriteInfoLine("Authenticated as " + token.FullName + ".");

			Uploader.UserId = token.UserId;

			string path;
			if (args != null && args.Length > 0)
			{
				path = args[0];
			}
			else
			{
				path = Environment.CurrentDirectory;
			}

			//path = @"C:\Users\Victor_Pryganov\Pictures\photos\06.20.09 Крымская слудка\";

			Uploader.Flickr = FlickrManager.GetAuthInstance();

			try
			{
				ProcessDirectory(path);
			}
			catch (Exception e)
			{
				ConsoleHelper.WriteErrorLine("\nUpload failed.");
				ConsoleHelper.WriteException(e);
			}
		}

		static OAuthAccessToken Authenticate()
		{
			OAuthAccessToken token = FlickrManager.OAuthToken;

			if (token == null || token.Token == null)
			{
				ConsoleHelper.WriteInfoLine("Requesting access token...");

				Flickr f = FlickrManager.GetInstance();
				var requestToken = f.OAuthGetRequestToken("oob");

				string url = f.OAuthCalculateAuthorizationUrl(requestToken.Token, AuthLevel.Write);

				Process.Start(url);

				ConsoleHelper.WriteInfo("Verifier: ");
				var verifier = Console.ReadLine();

				token = f.OAuthGetAccessToken(requestToken, verifier);
				FlickrManager.OAuthToken = token;
			}

			return token;
		}

		static void ProcessDirectory(string path)
		{
			if (!Directory.Exists(path))
			{
				ConsoleHelper.WriteErrorLine("The specified path is invalid.");
				return;
			}

			var files = FindPictureFiles(path);

			if (!files.Any())
			{
				ConsoleHelper.WriteWarningLine("Could not locate any files to upload in the directory: {0}.", path);
				return;
			}

			ConsoleHelper.WriteDebugLine("Processing files in the directory: {0}.", path);

			string photosetName = Path.GetFileName(Path.GetDirectoryName(files.First()));

			var photoset = Uploader.FindPhotosetByName(photosetName);
			bool photosetExists = photoset != null && photoset.Title == photosetName;

			List<Photo> photosetPhotos = photosetExists
				? Uploader.GetPhotosetPictures(photoset.PhotosetId)
				: new List<Photo>();

			if (photosetExists)
			{
				var totalFilesInDirectory = files.Count;
				files.RemoveAll(x => photosetPhotos.Any(p => p.Title == Path.GetFileNameWithoutExtension(x)));
				ConsoleHelper.WriteInfoLine("{0} out of {1} files are already in the existing photoset.", totalFilesInDirectory - files.Count, totalFilesInDirectory);
			}

			// Check again as the collection might have been modified
			if (!files.Any())
			{
				ConsoleHelper.WriteWarningLine("All files were already uploaded to the photoset. Nothing to do.");
				return;
			}

			ConsoleHelper.WriteInfo("Agree to upload {0} files to {1} photoset '{2}'?\nYes/No: ",
				files.Count,
				photosetExists ? "the existing" : "a new",
				photosetName);
			var answer = Console.ReadKey();
			if ("y" != answer.KeyChar.ToString(CultureInfo.InvariantCulture).ToLower())
			{
				return;
			}

			Console.WriteLine();

			#region Upload photos

			var failures = new ConcurrentDictionary<string, string>();
			var photoIds = new ConcurrentDictionary<string, string>();

			ConsoleHelper.WriteDebug("Progress: ");
			SaveCursorPosition();

			int uploaded = 0,
				totalToUpload = files.Count;

			ConsoleHelper.WriteDebug("{0} of {1}.", uploaded, totalToUpload);

			object locker = new object();

			while (files.Any())
			{
				// Take no more than {BatchSizeForParallelUpload} files for parallel processing
				var batch = files.Take(BatchSizeForParallelUpload).ToList();

				var uploadTasks = batch
					.Select(fileName =>
						Task.Factory.StartNew(() =>
						{
							var title = Path.GetFileNameWithoutExtension(fileName);
							try
							{
								// Check if picture is not in the photoset (if it exists)
								var photo = photosetPhotos.FirstOrDefault(x => x.Title == title); //?? Uploader.FindPictureByName(title);
								if (photo == null || photo.Title != title)
								{
									// No such picture found - uploading
									var photoId = Uploader.UploadPicture(fileName, title, null, null);
									if (!photoIds.TryAdd(photoId, title))
									{
										//uploaded twice?
										throw new Exception(String.Format("{0} is already in the list of uploaded files.", title));
									}
								}
							}
							catch (Exception e)
							{
								failures.TryAdd(fileName, e.Message);
							}

							files.Remove(fileName);

							lock (locker)
							{
								RestoreCursorPosition();
								ConsoleHelper.WriteDebug("{0} of {1}.", ++uploaded, totalToUpload);
							}
						}, TaskCreationOptions.LongRunning))
					.ToArray();

				// Wait for batch to be uploaded
				Task.WaitAll(uploadTasks);

				// Pause for 3 sec after each batch
				Thread.Sleep(3000);
			}

			SetCursorPosition(0);

			if (failures.Any())
			{
				ConsoleHelper.WriteErrorLine("Uploaded with errors:");
				foreach (var failure in failures)
				{
					ConsoleHelper.WriteWarning("{0,-20}: ", failure.Key);
					ConsoleHelper.WriteErrorLine(failure.Value);
				}
			}
			else
			{
				ConsoleHelper.WriteInfoLine("All files were successfully uploaded.");
			}

			#endregion

			if (!photoIds.Any())
			{
				ConsoleHelper.WriteWarningLine("No files were uploaded to '{0}'.", photosetName);
				return;
			}

			string photosetId;

			if (photosetExists)
			{
				photosetId = photoset.PhotosetId;
			}
			else
			{
				#region Create new photoset

				ConsoleHelper.WriteInfoLine("Creating photoset '{0}'...", photosetName);

				// Set the first photo in the set as its cover
				var coverPhotoId = photoIds.Keys.First();
				string coverPhotoName;
				photoIds.TryRemove(coverPhotoId, out coverPhotoName);

				// Create new photoset
				photoset = Uploader.CreatePhotoSet(photosetName, coverPhotoId);
				photosetId = photoset.PhotosetId;

				ConsoleHelper.WriteInfoLine("Photoset created.");

				#endregion
			}

			#region Move photos to the photoset

			ConsoleHelper.WriteInfoLine("Moving uploaded files to the photoset...");

			ConsoleHelper.WriteDebug("Progress: ");
			SaveCursorPosition();

			int moved = 0,
				totalToMove = photoIds.Count;

			Console.Write("{0} of {1}.", moved, totalToMove);

			// Move pictures to the photoset
			var movingTasks = photoIds
				.Select(x =>
					Task.Factory.StartNew(() =>
					{
						Uploader.AddPictureToPhotoSet(x.Key, photosetId);
						RestoreCursorPosition();
						ConsoleHelper.WriteDebug("{0} of {1}.", ++moved, totalToMove);
					}))
				.ToArray();

			Task.WaitAll(movingTasks);

			#endregion

			SetCursorPosition(0);
			ConsoleHelper.WriteInfoLine("Uploaded pictures were successfully moved to '{0}'.", photosetName);

			#region Validate photos in the photoset

			// Rescan the directory
			files = FindPictureFiles(path);

			// Get all photos in the photoset
			var photosetPhotoTitles = Uploader.GetPhotosetPictures(photosetId)
				.Select(p => p.Title)
				.ToList();

			// Find files which were not uploaded to the photoset
			var leftFiles = files.Select(Path.GetFileNameWithoutExtension).Where(x => !photosetPhotoTitles.Contains(x)).ToList();
			if (leftFiles.Any())
			{
				ConsoleHelper.WriteWarningLine("\nSome files were not uploaded:");
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
				ConsoleHelper.WriteWarningLine("\nSome files have duplicates:");
				foreach (var duplicate in duplicates)
				{
					ConsoleHelper.WriteWarningLine("{0,-20} x{1}", duplicate.Title, duplicate.Count);
				}
			}

			#endregion
		}

		public static List<string> FindPictureFiles(string directory)
		{
			return Directory.EnumerateFiles(directory)
				.Where(file => file.ToLower().EndsWith("jpg") || file.ToLower().EndsWith("jpeg"))
				.ToList();
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
