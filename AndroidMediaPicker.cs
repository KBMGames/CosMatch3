using System.Collections.Generic;
using UnityEngine;


public static class AndroidMediaPicker
{
    public struct MediaItem
    {
        public long id;
        public string name;
    }

#if UNITY_ANDROID && !UNITY_EDITOR

    private static List<MediaItem> QueryAndroid(bool images, int limit)
    {
        var result = new List<MediaItem>();
        try
        {
            var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null) return result;

            var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            string storeClass = images ? "android.provider.MediaStore$Images$Media" : "android.provider.MediaStore$Audio$Media";
            var uri = new AndroidJavaClass(storeClass).GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");

            string[] projection = { "_id", "display_name" };
            object[] args = { uri, (object)projection, null, "date_added DESC" };
            var cursor = resolver.Call<AndroidJavaObject>("query", args);
            if (cursor == null) return result;

            try
            {
                while (!cursor.Call<bool>("isClosed"))
                {
                    bool advanced = cursor.Call<bool>(result.Count == 0 ? "moveToFirst" : "moveToNext");
                    if (!advanced) break;
                    var item = new MediaItem();
                    item.id = cursor.Call<long>("getInt", 0);
                    item.name = cursor.Call<string>("getString", 1);
                    result.Add(item);
                    if (result.Count >= limit) break;
                }
            }
            finally
            {
                cursor.Call("close");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("AndroidMediaPicker: " + e.Message);
        }
        return result;
    }

    private static byte[] ReadBytesAndroid(bool images, long id)
    {
        try
        {
            var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null) return null;

            var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            string kind = images ? "images" : "audio";
            var uri = new AndroidJavaObject("android.net.Uri", "parse", "content://media/external/" + kind + "/media/" + id);
            var stream = resolver.Call<AndroidJavaObject>("openInputStream", uri);
            if (stream == null) return null;

            byte[] data = stream.Call<byte[]>("readAllBytes"); 
            stream.Call("close");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("AndroidMediaPicker: " + e.Message);
            return null;
        }
    }

#else

    private static List<MediaItem> QueryAndroid(bool images, int limit)
    {
        return new List<MediaItem>();
    }

    private static byte[] ReadBytesAndroid(bool images, long id)
    {
        return null;
    }

#endif

    
    public static List<MediaItem> Query(bool images, int limit)
    {
        return QueryAndroid(images, limit);
    }

    
    public static byte[] ReadBytes(bool images, long id)
    {
        return ReadBytesAndroid(images, id);
    }
}
