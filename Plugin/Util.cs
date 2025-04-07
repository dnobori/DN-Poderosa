// Copyright 2004-2017 The Poderosa Project.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;
using System.Collections;
using System.Text;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

//using Microsoft.JScript;
using System.CodeDom.Compiler;

using Poderosa.Boot;
using System.Drawing.Text;
using System.Collections.Generic;
using System.Reflection;

public static class FastFont3
{
    static readonly string WinFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
    static readonly string UserFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");

    static readonly PrivateFontCollection Col = new PrivateFontCollection();
    static readonly HashSet<string> FilenameHashSet = new HashSet<string>();

    static readonly Dictionary<string, FontFamily> NameToFontFamilyDict = new Dictionary<string, FontFamily>();
    static readonly Dictionary<string, Font> CachedFont = new Dictionary<string, Font>();

    static bool ResetFlag = true;

    static List<int> LangIDList = new List<int>();

    static FastFont3()
    {
        LangIDList.Add(0);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("en-us").LCID);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("ja-jp").LCID);

        // For console fonts
        LoadFontFile("msgothic.ttc");
        LoadFontFile("tahoma.ttf");
        LoadFontFile("PlemolJPHS-Regular.ttf");
        LoadFontFile("lucon.ttf");
    }

    public static void LoadFontFile(string fontFileName)
    {
        lock (FilenameHashSet)
        {
            if (FilenameHashSet.Contains(fontFileName) == false)
            {
                string fontPath1 = Path.Combine(WinFontsDirPath, fontFileName);
                string fontPath2 = Path.Combine(UserFontsDirPath, fontFileName);

                bool ok = false;

                try
                {
                    if (File.Exists(fontPath1))
                    {
                        Col.AddFontFile(fontPath1);
                        ok = true;
                    }
                    else if (File.Exists(fontPath2))
                    {
                        Col.AddFontFile(fontPath2);
                        ok = true;
                    }
                }
                catch
                {
                }

                if (ok)
                {
                    FilenameHashSet.Add(fontFileName);

                    lock (NameToFontFamilyDict)
                    {
                        ResetFlag = true;
                    }
                }
            }
        }
    }

    static void RebuildNameToFontFamilyDictIfNecessary()
    {
        lock (NameToFontFamilyDict)
        {
            if (ResetFlag)
            {
                NameToFontFamilyDict.Clear();

                foreach (var family in Col.Families)
                {
                    foreach (var lang in LangIDList)
                    {
                        string name = family.GetName(lang);

                        NameToFontFamilyDict[name] = family;
                    }
                }

                ResetFlag = false;
            }
        }
    }

    public static Font CreateFont(string fontName, float fontSize = 9.0f, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        RebuildNameToFontFamilyDictIfNecessary();

        if (NameToFontFamilyDict.TryGetValue(fontName, out var family) == false)
        {
            if (NameToFontFamilyDict.TryGetValue("MS UI Gothic", out var family2) == false)
            {
                throw new ApplicationException($"Font name '{fontName}' not found.");
            }

            family = family2;
        }

        return new Font(family, fontSize, style, unit);
    }

    public static Font GetCachedFont(string fontName, float fontSize = 9.0f, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        string key = $"{fontName}:{fontSize}:{style}:{unit}";

        lock (CachedFont)
        {
            if (CachedFont.TryGetValue(key, out var ret))
            {
                return ret;
            }
        }

        var ret2 = CreateFont(fontName, fontSize, style, unit);

        lock (CachedFont)
        {
            CachedFont[key] = ret2;
        }

        return ret2;
    }

    public static void SetFormsDefaultFontForBootSpeedUp(Font font)
    {
        {
            Type targetType = typeof(System.Windows.Forms.Control);

            FieldInfo fieldInfo = targetType.GetField(
                "defaultFont",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(null, font);
            }
        }
        {
            Type targetType = typeof(System.Windows.Forms.ToolStripManager);

            FieldInfo fieldInfo = targetType.GetField(
                "defaultFont",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(null, font);
            }
        }
    }
}

namespace Poderosa
{
    /// <summary>
    /// <ja>
    /// 標準的な成功／失敗を示します。
    /// </ja>
    /// <en>
    /// A standard success/failure is shown. 
    /// </en>
    /// </summary>
    public enum GenericResult
    {
        /// <summary>
        /// <ja>成功しました</ja>
        /// <en>Succeeded</en>
        /// </summary>
        Succeeded,
        /// <summary>
        /// <ja>失敗しました</ja>
        /// <en>Failed</en>
        /// </summary>
        Failed
    }

    //Debug.WriteLineIfあたりで使用
    /// <summary>
    /// 
    /// </summary>
    /// <exclude/>
    public static class DebugOpt
    {
#if DEBUG
        public static bool BuildToolBar = false;
        public static bool CommandPopup = false;
        public static bool DrawingPerformance = false;
        public static bool DumpDocumentRelation = false;
        public static bool IntelliSense = false;
        public static bool IntelliSenseMenu = false;
        public static bool LogViewer = false;
        public static bool Macro = false;
        public static bool MRU = false;
        public static bool PromptRecog = false;
        public static bool Socket = false;
        public static bool SSH = false;
        public static bool ViewManagement = false;
        public static bool WebBrowser = false;
#else //RELEASE
        public static bool BuildToolBar = false;
        public static bool CommandPopup = false;
        public static bool DrawingPerformance = false;
        public static bool DumpDocumentRelation = false;
        public static bool IntelliSense = false;
        public static bool IntelliSenseMenu = false;
        public static bool LogViewer = false;
        public static bool Macro = false;
        public static bool MRU = false;
        public static bool PromptRecog = false;
        public static bool Socket = false;
        public static bool SSH = false;
        public static bool ViewManagement = false;
        public static bool WebBrowser = false;
#endif
    }


    /// <summary>
    /// 
    /// </summary>
    /// <exclude/>
    public static class RuntimeUtil
    {
        public static void ReportException(Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);

            string errorfile = ReportExceptionToFile(ex);

            //メッセージボックスで通知。
            //だがこの中で例外が発生することがSP1ではあるらしい。しかもそうなるとアプリが強制終了だ。
            //Win32のメッセージボックスを出しても同じ。ステータスバーなら大丈夫のようだ
            try
            {
                string msg = String.Format(InternalPoderosaWorld.Strings.GetString("Message.Util.InternalError"), errorfile, ex.Message);
                MessageBox.Show(msg, "Poderosa", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            catch (Exception ex2)
            {
                Debug.WriteLine("(MessageBox.Show() failed) " + ex2.Message);
                Debug.WriteLine(ex2.StackTrace);
            }
        }
        public static void SilentReportException(Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
            ReportExceptionToFile(ex);
        }
        public static void DebuggerReportException(Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
        }
        //ファイル名を返す
        private static string ReportExceptionToFile(Exception ex)
        {
            string errorfile = null;
            //エラーファイルに追記
            StreamWriter sw = null;
            try
            {
                sw = GetErrorLog(ref errorfile);
                ReportExceptionToStream(ex, sw);
            }
            finally
            {
                if (sw != null)
                    sw.Close();
            }
            return errorfile;
        }
        private static void ReportExceptionToStream(Exception ex, StreamWriter sw)
        {
            sw.WriteLine(DateTime.Now.ToString());
            sw.WriteLine(ex.Message);
            sw.WriteLine(ex.StackTrace);
            //inner exceptionを順次
            Exception i = ex.InnerException;
            while (i != null)
            {
                sw.WriteLine("[inner] " + i.Message);
                sw.WriteLine(i.StackTrace);
                i = i.InnerException;
            }
        }
        private static StreamWriter GetErrorLog(ref string errorfile)
        {
            errorfile = PoderosaStartupContext.Instance.ProfileHomeDirectory + "error.log";
            return new StreamWriter(errorfile, true/*append!*/, Encoding.Default);
        }

        public static Font CreateFont(string name, float size)
        {
            try
            {
                return FastFont3.CreateFont(name, size);
                //return new Font(name, size);
            }
            catch (ArithmeticException)
            {
                //JSPagerの件で対応。msvcr71がロードできない環境もあるかもしれないので例外をもらってはじめて呼ぶようにする
                Win32.ClearFPUOverflowFlag();
                return new Font(name, size);
            }
        }

        public static string ConcatStrArray(string[] values, char delimiter)
        {
            StringBuilder bld = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    bld.Append(delimiter);
                bld.Append(values[i]);
            }
            return bld.ToString();
        }

        //min未満はmin, max以上はmax、それ以外はvalueを返す
        public static int AdjustIntRange(int value, int min, int max)
        {
            Debug.Assert(min <= max);
            if (value < min)
                return min;
            else if (value > max)
                return max;
            else
                return value;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exclude/>
    public static class ParseUtil
    {
        public static bool ParseBool(string value, bool defaultvalue)
        {
            try
            {
                if (value == null || value.Length == 0)
                    return defaultvalue;
                return Boolean.Parse(value);
            }
            catch (Exception)
            {
                return defaultvalue;
            }
        }
        public static byte ParseByte(string value, byte defaultvalue)
        {
            try
            {
                if (value == null || value.Length == 0)
                    return defaultvalue;
                return Byte.Parse(value);
            }
            catch (Exception)
            {
                return defaultvalue;
            }
        }
        public static int ParseInt(string value, int defaultvalue)
        {
            try
            {
                if (value == null || value.Length == 0)
                    return defaultvalue;
                return Int32.Parse(value);
            }
            catch (Exception)
            {
                return defaultvalue;
            }
        }
        public static float ParseFloat(string value, float defaultvalue)
        {
            try
            {
                if (value == null || value.Length == 0)
                    return defaultvalue;
                return Single.Parse(value);
            }
            catch (Exception)
            {
                return defaultvalue;
            }
        }
        public static int ParseHexInt(string value, int defaultvalue)
        {
            try
            {
                if (value == null || value.Length == 0)
                    return defaultvalue;
                return Int32.Parse(value, System.Globalization.NumberStyles.HexNumber);
            }
            catch (Exception)
            {
                return defaultvalue;
            }
        }
        public static Color ParseColor(string t, Color defaultvalue)
        {
            if (t == null || t.Length == 0)
                return defaultvalue;
            else
            {
                if (t.Length == 8)
                { //16進で保存されていることもある。窮余の策でこのように
                    int v;
                    if (Int32.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out v))
                        return Color.FromArgb(v);
                }
                else if (t.Length == 6)
                {
                    int v;
                    if (Int32.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out v))
                        return Color.FromArgb((int)((uint)v | 0xFF000000)); //'A'要素は0xFFに
                }
                Color c = Color.FromName(t);
                return c.ToArgb() == 0 ? defaultvalue : c; //へんな名前だったとき、ARGBは全部0になるが、IsEmptyはfalse。なのでこれで判定するしかない
            }
        }

        public static T ParseEnum<T>(string value, T defaultvalue)
        {
            try
            {
                if (value == null || value.Length == 0)
                    return defaultvalue;
                else
                    return (T)Enum.Parse(typeof(T), value, false);
            }
            catch (Exception)
            {
                return defaultvalue;
            }
        }

        public static bool TryParseEnum<T>(string value, ref T parsed)
        {
            if (value == null || value.Length == 0)
            {
                return false;
            }

            try
            {
                parsed = (T)Enum.Parse(typeof(T), value, false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        //TODO Generics化
        public static ValueType ParseMultipleEnum(Type enumtype, string t, ValueType defaultvalue)
        {
            try
            {
                int r = 0;
                foreach (string a in t.Split(','))
                    r |= (int)Enum.Parse(enumtype, a, false);
                return r;
            }
            catch (FormatException)
            {
                return defaultvalue;
            }
        }
    }

}
