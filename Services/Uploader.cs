using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FlickrNet;

namespace YetAnotherFlickrUploader.Services
{
	public static class Uploader
	{
		public static Flickr Flickr;
		public static string UserId;

		public static string UploadPicture(string path, string title, string description, string tags)
		{
			Flickr.OnUploadProgress += OnUploadProgress;
			string photoId = TryExecute(
				() => Flickr.UploadPicture(path, title, description, tags, false, false, false),
				() => {
					var photo = FindPictureNotInSet(title);
					return photo != null && !string.IsNullOrEmpty(photo.PhotoId) ? photo.PhotoId : null;
				});
			Flickr.OnUploadProgress -= OnUploadProgress;

			return photoId;
		}

		public static Photo FindPictureNotInSet(string title)
		{
			var photos = Flickr.PhotosGetNotInSet(new PartialSearchOptions());
			return photos.FirstOrDefault(x => x.Title == title);
			//var photos = Flickr.PhotosSearch(new PhotoSearchOptions { UserId = UserId, Text = title });
			//return photos.FirstOrDefault();
		}

		public static Photo FindPictureByName(string title)
		{
			var photos = Flickr.PhotosSearch(new PhotoSearchOptions { UserId = UserId, Text = title });
			return photos.FirstOrDefault();
		}

		public static Photoset CreatePhotoSet(string title, string coverPhotoId)
		{
			var f = FlickrManager.GetAuthInstance();
			var photoset = TryExecute(
				() => f.PhotosetsCreate(title, coverPhotoId),
				() => FindPhotosetByName(title)
				);
			return photoset;
		}

		public static Photoset FindPhotosetByName(string title)
		{
			var photoSets = Flickr.PhotosetsGetList(UserId);
			return photoSets.FirstOrDefault(x => x.Title == title);
		}

		public static List<Photo> GetPhotosetPictures(string photosetId)
		{
			var photos = new List<Photo>();
			// Get photoset size from its properties
			Photoset photoset = Flickr.PhotosetsGetInfo(photosetId);
			// Get photos page by page
			int pageNumber = 0;
			int photosPerPage = 500; // Max. allowed value
			while (pageNumber*photosPerPage < photoset.NumberOfPhotos)
			{
				var page = Flickr.PhotosetsGetPhotos(photosetId,
					PhotoSearchExtras.AllUrls | PhotoSearchExtras.Description | PhotoSearchExtras.Tags | PhotoSearchExtras.DateTaken | PhotoSearchExtras.DateUploaded,
					++pageNumber,
					photosPerPage);
				photos.AddRange(page.ToList());
			}
			return photos;
		}

		public static void AddPictureToPhotoSet(string photoId, string photosetId)
		{
			TryExecute(
				() => { Flickr.PhotosetsAddPhoto(photosetId, photoId); return photoId; },
				() => FindPictureInPhotoset(photoId, photosetId));
		}

		public static string FindPictureInPhotoset(string photoId, string photosetId)
		{
			var photos = GetPhotosetPictures(photosetId);
			return photos.Where(x => x.PhotoId == photoId).Select(x => x.PhotoId).FirstOrDefault();
		}

		public static void SetPhotoUploadDate(string photoId, DateTime datePosted)
		{
			Flickr.PhotosSetDates(photoId, datePosted);
		}

		static void OnUploadProgress(object sender, UploadProgressEventArgs e)
		{
			if (!e.UploadComplete && e.ProcessPercentage > 0)
			{
				//Console.CursorLeft = 30;
				//Console.Write("{0}%", e.ProcessPercentage);
			}
		}

		static T TryExecute<T>(Func<T> operation, Func<T> verify, int maxRetries = 5, int delay = 1000)
			where T : class
		{
			T result = default(T);

			int retries = 0;
			while (retries < maxRetries)
			{
				try
				{
					retries += 1;
					result = operation.Invoke();
					break;
				}
				catch (Exception)
				{
					result = verify.Invoke();
					if (result != null)
						break;

					if (retries == maxRetries)
						throw;
					Thread.Sleep(delay);
				}
			}

			return result;
		}

	}
}
