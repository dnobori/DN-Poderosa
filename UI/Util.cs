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
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

public static class FastFont2
{
    static readonly string WinFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
    static readonly string UserFontsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");

    static readonly PrivateFontCollection Col = new PrivateFontCollection();
    static readonly HashSet<string> FilenameHashSet = new HashSet<string>();

    static readonly Dictionary<string, FontFamily> NameToFontFamilyDict = new Dictionary<string, FontFamily>();
    static readonly Dictionary<string, Font> CachedFont = new Dictionary<string, Font>();

    static bool ResetFlag = true;

    static List<int> LangIDList = new List<int>();

    static FastFont2()
    {
        LangIDList.Add(0);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("en-us").LCID);
        LangIDList.Add(System.Globalization.CultureInfo.GetCultureInfo("ja-jp").LCID);

        LoadFontFile("msgothic.ttc");
        LoadFontFile("tahoma.ttf");
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

namespace Poderosa.UI {
    /// <summary>
    /// 
    /// </summary>
    /// <exclude/>
    public class UIUtil {
        public static int AdjustRange(int value, int min, int max) {
            if (value < min)
                value = min;
            else if (value > max)
                value = max;

            return value;
        }

        public static void ReplaceControl(Control parent, Control src, Control dest) {
            Debug.Assert(src.Parent == parent);
            Size size = src.Size;
            DockStyle dock = src.Dock;
            Point location = src.Location;

            Control[] t = new Control[parent.Controls.Count];
            for (int i = 0; i < t.Length; i++) {
                Control c = parent.Controls[i];
                t[i] = c == src ? dest : c;
            }
            dest.Dock = dock;
            dest.Size = size;
            dest.Location = location;
            parent.Controls.Clear();
            parent.Controls.AddRange(t);
            Debug.Assert(parent.Controls.Contains(dest));
        }

        public static void DumpControlTree(Control t) {
            DumpControlTree(t, 0);
        }

        private static void DumpControlTree(Control t, int indent) {
            StringBuilder bld = new StringBuilder();
            for (int i = 0; i < indent; i++)
                bld.Append(' ');
            bld.Append(t.GetType().Name);
            bld.Append(" Size=");
            bld.Append(t.Size.ToString());
            bld.Append(" Dock=");
            bld.Append(t.Dock.ToString());
            Debug.WriteLine(bld.ToString());
            foreach (Control c in t.Controls)
                DumpControlTree(c, indent + 1);
        }
    }

    /// <summary>
    /// Utility methods for creating icon.
    /// </summary>
    public static class IconUtil {

        /// <summary>
        /// Creates a monotone icon from alpha channel of the specified image and the specified color.
        /// </summary>
        /// <param name="mask">image which has alpha channel</param>
        /// <param name="color">icon color</param>
        /// <returns>new icon image</returns>
        public static Bitmap CreateColoredIcon(Image mask, Color color) {
            Bitmap bmp = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppArgb);
            var rval = color.R / 255f;
            var gval = color.G / 255f;
            var bval = color.B / 255f;
            using (var attr = new ImageAttributes()) {
                attr.SetColorMatrix(new ColorMatrix(new float[][] {
                    new float[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f },
                    new float[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f },
                    new float[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f },
                    new float[] { 0.0f, 0.0f, 0.0f, 1.0f, 0.0f },
                    new float[] { rval, gval, bval, 0.0f, 1.0f },
                }));
                using (var g = Graphics.FromImage(bmp)) {
                    g.DrawImage(mask, new Rectangle(0, 0, mask.Width, mask.Height), 0, 0, mask.Width, mask.Height, GraphicsUnit.Pixel, attr);
                }
            }
            return bmp;
        }


    }

}
