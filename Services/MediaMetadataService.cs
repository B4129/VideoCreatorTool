using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// Windows Shell APIを使用してメディアファイルのメタ情報を取得するサービス
    /// </summary>
    public static class MediaMetadataService
    {
        public class MediaMetadata
        {
            public double? Duration { get; set; }
            public int? Width { get; set; }
            public int? Height { get; set; }
        }

        // Shell API P/Invoke
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport]
        [Guid("000214F2-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellFolder
        {
            void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
            void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
            void BindToObject(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);
            void BindToStorage(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);
            void CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
            void CreateViewObject(IntPtr hwndOwner, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);
            void GetAttributesOf(uint cidl, IntPtr apidl, ref uint rgfInOut);
            void GetUIObjectOf(IntPtr hwndOwner, uint cidl, IntPtr apidl, [In] ref Guid riid, ref uint rgfReserved, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);
            void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
            void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
        }

        private static readonly Guid IID_IShellFolder = new Guid("000214E6-0000-0000-C000-000000000046");
        private static readonly Guid FOLDERID_Desktop = new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

        /// <summary>
        /// メディアファイルのメタ情報を非同期で取得
        /// </summary>
        public static Task<MediaMetadata?> GetMetadataAsync(string filePath, Models.MediaType mediaType)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        Debug.WriteLine($"ファイルが見つかりません: {filePath}");
                        return null;
                    }

                    var metadata = new MediaMetadata();

                    // Shell APIを使用してメタ情報を取得
                    var shellType = Type.GetTypeFromProgID("Shell.Application");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType);
                        var folderPath = Path.GetDirectoryName(filePath);
                        var fileName = Path.GetFileName(filePath);

                        if (folderPath != null && fileName != null)
                        {
                            var folder = shell.NameSpace(folderPath);
                            if (folder != null)
                            {
                                var item = folder.ParseName(fileName);
                                if (item != null)
                                {
                                    // Duration (Property 27: "長さ")
                                    var durationStr = folder.GetDetailsOf(item, 27);
                                    if (!string.IsNullOrEmpty(durationStr))
                                    {
                                        if (TimeSpan.TryParse(durationStr, out TimeSpan duration))
                                        {
                                            metadata.Duration = duration.TotalSeconds;
                                        }
                                    }

                                    //Dimensions (Property 31: "寸法") - 画像/動画の場合
                                    if (mediaType == Models.MediaType.Image || mediaType == Models.MediaType.Video)
                                    {
                                        var dimensionsStr = folder.GetDetailsOf(item, 31);
                                        if (!string.IsNullOrEmpty(dimensionsStr))
                                        {
                                            // "1920 x 1080" 形式を解析
                                            var parts = dimensionsStr.Split('x');
                                            if (parts.Length == 2)
                                            {
                                                if (int.TryParse(parts[0].Trim(), out int width))
                                                {
                                                    metadata.Width = width;
                                                }
                                                var heightStr = parts[1].Replace("ピクセル", "").Trim();
                                                if (int.TryParse(heightStr, out int height))
                                                {
                                                    metadata.Height = height;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    return metadata;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"メタ情報取得エラー: {ex.Message}");
                    return null;
                }
            });
        }
    }
}
