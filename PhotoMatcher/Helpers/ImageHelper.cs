using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PhotoMatcher.Helpers
{
    public static class ImageHelper
    {
        private static readonly string[] Extensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".bmp"
        };

        public static List<string> GetImages(string folder, bool recursive = false)
        {
            var searchOption = recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            return Directory
                .GetFiles(folder, "*", searchOption)
                .Where(f => Extensions.Contains(
                    Path.GetExtension(f).ToLower()))
                .ToList();
        }
    }
}