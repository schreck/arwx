using System.Collections;
using System.Xml;
using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldFile
{
    public Vector2 Corner;
    public Vector2 Scale;
    //public Vector2 Rotation; //TODO
    public WorldFile(float c_x, float c_y, float s_x, float s_y)
    {
        Corner = new Vector2(c_x, c_y);
        Scale  = new Vector2(s_x, s_y);
    }
}

public class radar_history {
    private List<Texture2D> T;
    private List<DateTime> TS;

    public radar_history()
    {
        T = new List<Texture2D>();
        TS = new List<DateTime>();
    }

    public void add(Texture2D t,DateTime ts)
    {
        T.Add(t);
        TS.Add(ts);
    }

    public void add(Texture2D t, string fname)
    {
        var S = fname.Split('_');
        // wrong length? exception
        if (S.Length < 3)
        {
            Debug.Log("wrong! " + fname); // find this higher up?
            //add(t, new DateTime());
        }
        else {
            var ts = new DateTime(
                Int32.Parse(S[1].Substring(0, 4)),
                Int32.Parse(S[1].Substring(4, 2)),
                Int32.Parse(S[1].Substring(6, 2)),
                Int32.Parse(S[2].Substring(0, 2)),
                Int32.Parse(S[2].Substring(2, 2)),
                0);
            add(t, ts);
        }
    }

    public Texture2D asof(DateTime now) // returns texture asof time (now)
    {
        var ts_0 = new TimeSpan(0, 0, 0);
        var min = new TimeSpan(888, 8, 8, 8, 8); // HACK45 start from first interval?
        int i_min = -1;
        for(int i=0;i<TS.Count; i++)
        {
            // now - then
            var foo = now - TS[i];
            if (foo > ts_0) // i is in the past
            {
                if (foo < min) // closest without going over
                {
                    min = foo;
                    i_min = i;
                }
            }
        }
        return T[i_min];
    }

    public int length()
    {
        return TS.Count;
    }
}

public class radar_plain : MonoBehaviour {

    private Transform plane; // child object 
    private string station;
    private static string radar_base = "https://radar.weather.gov/ridge/RadarImg/N0R/";
    // offset objects
    private WorldFile gfw; // world file data
    private Vector2 gps; // gps data
    private Vector2Int wh; // width and height of image download pixels
    private radar_history rh; // stores historical radar textures
    private Texture2D now_image; // the image from now.
    private Texture2D last_converted; // the last image converted.

    void Start () {
        rh = new radar_history();
    }

    private string RadarURL()
    {
        return radar_base + station + "_N0R_0";
    }

    private string RadarHistDir()
    {
        return radar_base + station;
    }

    public string StatusDisplay()
    {
        bool loc = (gps.x != 0f);
        bool img = (wh.x != 0);
        bool world = (gfw != null);
        return (loc?"L":".")+(img?"I":".")+(world?"W":".")+"h(<i>"+rh.length()+"</i>)";
    }

    public int HistoryCount()
    {
        return rh.length();
    }

    public void LoadRadarData(string station_)
    {
        station = station_;
        var url = RadarURL();
        gps.x = 0f;
        wh.x = 0;
        gfw = null;
        StartCoroutine(LoadRadarDataEnum(url));
    }

    IEnumerator LoadRadarDataEnum(string url) {
        yield return StartCoroutine(world(url + ".gfw"));
        yield return StartCoroutine(img(url + ".gif"));
        yield return StartCoroutine(load_history(RadarHistDir()));
    }

    public void SetGps(LocationInfo loc)
    {
        gps = new Vector2(loc.longitude, loc.latitude);
        UpdateOffset();
    }

    public void SetGps(Vector2 loc)
    {
        gps = loc;
        UpdateOffset();
    }

    public void MockWH(Vector2Int wh_)
    {
        wh = wh_;
        UpdateOffset();
    }

    private static Color32 ConvertAndroidColor(int aCol)
    {
        Color32 c;
        c.b = (byte)((aCol) & 0xFF);
        c.g = (byte)((aCol >> 8) & 0xFF);
        c.r = (byte)((aCol >> 16) & 0xFF);
        c.a = (byte)((aCol >> 24) & 0xFF);
        return c;
    }

    IEnumerator load_history_element(string url,List<string> fnames)
    {
        foreach(string name in fnames)
        {
            if (name.Substring(0, 3).Equals(station)) // skip bad fnames
            {
                WWW www = new WWW(url + "/" + name);
                yield return www;
                //Debug.Log("Converter coroutine "+name);
                yield return StartCoroutine(GifToTextureAndroid(www.bytes, www.bytesDownloaded));
                //Debug.Log("Converter done      " + name);
                rh.add(last_converted, name);
            }
        }
    }

    IEnumerator load_history(string url)
    {
        WWW www = new WWW(url+"/?F=0");
        yield return www;
        XmlDocument xmlDoc = new XmlDocument();
        var text = www.text.Insert(54, " \"\""); // HACK43 (https://stackoverflow.com/a/9225499) relax the xml parser?
        xmlDoc.LoadXml(text);
        yield return null;
        var names = new List<string>(0);
        foreach (XmlElement node in xmlDoc.GetElementsByTagName("a"))
        {
            names.Add(node.Attributes["href"].Value);
        }
        yield return null;
        StartCoroutine(load_history_element(url, names));
        yield return null;
    }

    IEnumerator world(string url)
    {
        WWW www = new WWW(url);
        yield return www;
        string[] S_gfw = www.text.Split('\n'); // stringed gfw file
        if(S_gfw.Length == 7)
        {
            bool worked = true;
            float x_scale = 0f;
            float y_scale = 0f;
            float x = 0f;
            float y = 0f;
            worked = worked&&float.TryParse(S_gfw[0], out x_scale);
            //worked = worked && float.TryParse(S_gfw[1], out a);
            //worked = worked && float.TryParse(S_gfw[2], out b);
            worked = worked && float.TryParse(S_gfw[3], out y_scale);
            worked = worked && float.TryParse(S_gfw[4], out x);
            worked = worked && float.TryParse(S_gfw[5], out y);
            if (!worked)
            {
                Debug.Log("couldnt parse world file");
            }
            gfw = new WorldFile(x, y, x_scale, y_scale);
            UpdateOffset();
        }
        else
        {
            Debug.Log("wrong length in gfw? "+S_gfw.Length);
        }
    }

    IEnumerator img(string url)
    {
        WWW www = new WWW(url);
        yield return www;
        plane = transform.GetChild(0);
        Debug.Log(station+" : image load ");
        yield return StartCoroutine(GifToTextureAndroid(www.bytes, www.bytesDownloaded));
        Debug.Log(station + " : image loaded ");
        now_image = last_converted;
        plane.GetComponent<Renderer>().material.mainTexture = now_image;
        UpdateOffset();
        yield break;
    }

    private int[] GifToIntAA(byte[] bytes, int length)
    {
        AndroidJavaClass bmf = new AndroidJavaClass("android.graphics.BitmapFactory");
        AndroidJavaClass bm = new AndroidJavaClass("android.graphics.Bitmap");
        // this bitmapfactory class method returns a Bitmap object
        AndroidJavaObject bmo = bmf.CallStatic<AndroidJavaObject>("decodeByteArray", new object[] { bytes, 0, length });
        // we can figure out the width and height of the gif data
        int h = bmo.Call<int>("getHeight", new object[] { });
        int w = bmo.Call<int>("getWidth", new object[] { });
        wh = new Vector2Int(w, h); // set the global wh for offsetment
                                   // the trick is getting the pixels without calling the JNI to often i.e. _getPixel()_
                                   // setup java inputs for BitMap.getPixels
        System.IntPtr pixs = AndroidJNI.NewIntArray(h * w);
        jvalue[] gpargs;
        gpargs = new jvalue[7];
        gpargs[0].l = pixs;
        gpargs[1].i = 0;
        gpargs[2].i = w;
        gpargs[3].i = 0;
        gpargs[4].i = 0;
        gpargs[5].i = w;
        gpargs[6].i = h;
        // this is the same as `bmo.getPixels(pixs,0,w,0,0,w,h)` but in raw AndroidJNI calls because pixs is a pointer to an int[] buffer
        AndroidJNI.CallVoidMethod(bmo.GetRawObject(), AndroidJNI.GetMethodID(bm.GetRawClass(), "getPixels", "([IIIIIII)V"), gpargs);
        return AndroidJNI.FromIntArray(pixs);
    }

    IEnumerator GifToTextureAndroid(byte[] bytes,int length)
    {
        // peformance (http://altdevblog.com/2011/07/07/unity3d-coroutines-in-detail/)
        //   need to make sure the compute between yields is less than 33 ms
        var apixs = GifToIntAA(bytes, length);
        yield return "hello";
        // paint a texture with the pixels
        last_converted = new Texture2D(wh.x, wh.y, TextureFormat.ARGB32, false);
        for (int i = 0; i < wh.y; i++)
        {
            for (int j = 0; j < wh.x; j++)
            {
                int pixel = apixs[j + wh.x * i];
                Color32 pc = ConvertAndroidColor(pixel);
                last_converted.SetPixel(j, i, pc);
            }
            if(0==i%50)
                yield return null;
        }
        last_converted.Apply();
        yield break;
    }

    void UpdateOffset()
    {
        bool loaded = (gps.x != 0f) && (wh.x != 0) && (gfw != null); // gps loaded, image width height loaded, and world file loaded 
        // TODO promises?
        if (loaded)
        {
            // lat
            float plane_w = 10f * transform.localScale.x;
            float dx_dg = gps.x - gfw.Corner.x; // distance from top right corner to gps lon (dg)
            float dx_norm = dx_dg / (wh.x * gfw.Scale.x);
            //float dx_game = dx_norm * plane_w;
            // lon
            float plane_h = 10f * transform.localScale.y;
            float dy_dg = (-gps.y) + gfw.Corner.y;
            float dy_norm =  dy_dg / (wh.y * gfw.Scale.y);
            //float dy_game = dy_norm * plane_h;
            
            transform.position = new Vector3(
                 plane_w*(.5f - dx_norm),
                transform.position.y,
                plane_h*((-.5f)-dy_norm));
        }
    }

    void Update () {
    }
}