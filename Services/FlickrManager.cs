using FlickrNet;

namespace YetAnotherFlickrUploader.Services
{
	public class FlickrManager
	{
		public const string ApiKey = "7f148aeb1785cb0d9d0dac171e201bcc";
		public const string SharedSecret = "4c6d48ffe81b3ac6";

		public static Flickr GetInstance()
		{
			return new Flickr(ApiKey, SharedSecret);
		}

		public static Flickr GetAuthInstance()
		{
			var f = new Flickr(ApiKey, SharedSecret);
			f.OAuthAccessToken = OAuthToken.Token;
			f.OAuthAccessTokenSecret = OAuthToken.TokenSecret;
			return f;
		}

		public static OAuthAccessToken OAuthToken
		{
			get
			{
				return YetAnotherFlickrUploader.Properties.Settings.Default.OAuthToken;
			}
			set
			{
				YetAnotherFlickrUploader.Properties.Settings.Default.OAuthToken = value;
				YetAnotherFlickrUploader.Properties.Settings.Default.Save();
			}
		}
	}
}
