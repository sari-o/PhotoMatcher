using PhotoMatcher.Helpers;

namespace PhotoMatcher.Services
{
    public class ImageLoader
    {
        public List<string> Load(string folder, bool recursive = false)
        {
            return ImageHelper.GetImages(folder, recursive);
        }
    }
}