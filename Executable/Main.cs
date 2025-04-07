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

#if EXECUTABLE
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Poderosa.Util;
using Poderosa.Boot;
using Poderosa.Plugins;

using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

using System.Text.RegularExpressions;

public static class FastFont
{
    static readonly string WinFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
    static readonly string UserFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");

    static readonly PrivateFontCollection Col = new PrivateFontCollection();
    static readonly HashSet<string> FilenameHashSet = new HashSet<string>();

    static readonly Dictionary<string, FontFamily> NameToFontFamilyDict = new Dictionary<string, FontFamily>();
    static readonly Dictionary<string, Font> CachedFont = new Dictionary<string, Font>();

    static bool ResetFlag = true;

    static List<int> LangIDList = new List<int>();

    static FastFont()
    {
        LangIDList.Add(0);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("en-us").LCID);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("ja-jp").LCID);

        // For system-wide dialog fonts
        LoadFontFile("msgothic.ttc");
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

namespace Poderosa.Executable {
    internal class Root {
        private static IPoderosaApplication _poderosaApplication;

        public static void Run(string[] args) {
#if MONOLITHIC
            _poderosaApplication = PoderosaStartup.CreatePoderosaApplication(args, true);
#else
            _poderosaApplication = PoderosaStartup.CreatePoderosaApplication(args);
#endif
            if (_poderosaApplication != null) //アプリケーションが作成されなければ
                _poderosaApplication.Start();
        }

        //実行開始
        [STAThread]
        public static void Main(string[] args) {
            Application.SetCompatibleTextRenderingDefault(true);
            //FastFont.LoadFontFile("YuGothR.ttc");
            FastFont.SetFormsDefaultFontForBootSpeedUp(FastFont.CreateFont("MS UI Gothic"));

            try
            {
                Run(args);
            }
            catch (Exception ex) {
                RuntimeUtil.ReportException(ex);
            }
        }
    }

}
#endif