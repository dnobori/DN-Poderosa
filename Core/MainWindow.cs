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
using System.Collections.Generic;
using System.Collections;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Poderosa.Util;
using Poderosa.Plugins;
using Poderosa.Boot;
using Poderosa.Document;
using Poderosa.Sessions;
using Poderosa.Commands;
using Poderosa.UI;
using Poderosa.View;
using Poderosa.Util.Collections;
using System.Reflection;
using System.IO;

namespace Poderosa.Forms {
    //メインウィンドウ
    internal class MainWindow : PoderosaForm, IPoderosaMainWindow {

        private IViewManager _viewManager;
        private MainWindowArgument _argument;
        private MenuStrip _mainMenu;
        private TabBarTable _tabBarTable;
        private PoderosaToolStripContainer _toolStripContainer;
        private PoderosaStatusBar _statusBar;
        private TabBarManager _tabBarManager;

        public MainWindow(MainWindowArgument arg, MainWindowMenu menu) {
            _argument = arg;
            Debug.Assert(_argument != null);
            _commandKeyHandler.AddLastHandler(new FixedShortcutKeyHandler(this));

            this.ImeMode = ImeMode.NoControl;
            this.AllowDrop = true;

            this.Padding = new Padding(1, 0, 1, 0);

            arg.ApplyToUnloadedWindow(this);

            InitContent();

            ReloadMenu(menu, true);
        }

        private void InitContent() {
            this.SuspendLayout();

            IExtensionPoint creator_ext = WindowManagerPlugin.Instance.PoderosaWorld.PluginManager.FindExtensionPoint(WindowManagerConstants.MAINWINDOWCONTENT_ID);
            IViewManagerFactory f = ((IViewManagerFactory[])creator_ext.GetExtensions())[0];

            _toolStripContainer = new PoderosaToolStripContainer(this, _argument.ToolBarInfo);
            this.Controls.Add(_toolStripContainer);

            //ステータスバーその他の初期化
            //コントロールを追加する順番は重要！
            _viewManager = f.Create(this);
            Control main = _viewManager.RootControl;
            if (main != null) { //テストケースではウィンドウの中身がないこともある
                main.Dock = DockStyle.Fill;
                _toolStripContainer.ContentPanel.Controls.Add(main);
            }
            int rowcount = _argument.TabRowCount;
            _tabBarTable = new TabBarTable();
            _tabBarTable.Height = rowcount * TabBarTable.ROW_HEIGHT;
            _tabBarTable.Dock = DockStyle.Top;
            _tabBarManager = new TabBarManager(_tabBarTable);

            _statusBar = new PoderosaStatusBar();

            _toolStripContainer.ContentPanel.Controls.Add(_tabBarTable);
            this.Controls.Add(_statusBar); //こうでなく、_toolStripContainer.BottomToolStripPanelに_statusBarを追加してもよさそうだが、そうするとツールバー項目がステータスバーの上下に挿入可能になってしまう

            _tabBarTable.Create(rowcount);

            this.ResumeLayout();
        }

        public PoderosaToolStripContainer ToolBarInternal {
            get {
                return _toolStripContainer;
            }
        }

        #region IPoderosaMainWindow & IPoderosaForm
        public IViewManager ViewManager {
            get {
                return _viewManager;
            }
        }
        public IDocumentTabFeature DocumentTabFeature {
            get {
                return _tabBarManager;
            }
        }
        public IContentReplaceableView LastActivatedView {
            get {
                IPoderosaDocument doc = _tabBarManager.ActiveDocument;
                if (doc == null)
                    return null;
                else
                    return SessionManagerPlugin.Instance.FindDocumentHost(doc).LastAttachedView as IContentReplaceableView;
            }
        }
        public IToolBar ToolBar {
            get {
                return _toolStripContainer;
            }
        }
        public IPoderosaStatusBar StatusBar {
            get {
                return _statusBar;
            }
        }

        #endregion


        protected override void OnLoad(EventArgs e) {
            //NOTE なぜかは不明だが、ウィンドウの位置はForm.Show()の呼び出し前に指定しても無視されて適当な位置が設定されてしまう。
            //なのでここで行うようにした。
            _argument.ApplyToLoadedWindow(this);
            base.OnLoad(e);
            //通知 クローズ時はWindowManagerが登録するイベントハンドラから
            WindowManagerPlugin.Instance.NotifyMainWindowLoaded(this);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
            base.OnClosing(e);
            try {
                if (SessionManagerPlugin.Instance == null)
                    return; //単体テストではSessionなしで起動することもありだ

                CommandResult r = CommandManagerPlugin.Instance.Execute(BasicCommandImplementation.CloseAll, this);
                if (r == CommandResult.Cancelled) {
                    _closeCancelled = true;
                    e.Cancel = true;
                }
                else
                    e.Cancel = false;
            }
            catch (Exception ex) {
                RuntimeUtil.ReportException(ex);
                e.Cancel = false; //バグのためにウィンドウを閉じることもできない、というのはまずい
            }
        }



        public void ReloadMenu(MainWindowMenu menu, bool with_toolbar) {
            this.SuspendLayout();
            if (_mainMenu != null)
                this.Controls.Remove(_mainMenu);
            _mainMenu = new MenuStrip();
            menu.FullBuild(_mainMenu, this);
            this.MainMenuStrip = _mainMenu;
            this.Controls.Add(_mainMenu);

            if (with_toolbar && _toolStripContainer != null)
                _toolStripContainer.Reload();

            this.ResumeLayout();
        }
        public void ReloadPreference(ICoreServicePreference pref) {
            IPoderosaAboutBoxFactory af = AboutBoxUtil.GetCurrentAboutBoxFactory();
            if (af != null)
                this.Icon = af.ApplicationIcon;
            _toolStripContainer.ReloadPreference(pref);
        }

        protected override void OnDragEnter(DragEventArgs args) {
            base.OnDragEnter(args);
            try {
                WindowManagerPlugin.Instance.BypassDragEnter(this, args);
            }
            catch (Exception ex) {
                RuntimeUtil.ReportException(ex);
            }
        }
        protected override void OnDragDrop(DragEventArgs args) {
            base.OnDragDrop(args);
            try {
                WindowManagerPlugin.Instance.BypassDragDrop(this, args);
            }
            catch (Exception ex) {
                RuntimeUtil.ReportException(ex);
            }
        }
    }

    internal class TabBarManager : IDocumentTabFeature, TabBarTable.IUIHandler {
        private TabBarTable _tabBarTable;

        public TabBarManager(TabBarTable table) {
            _tabBarTable = table;
            _tabBarTable.AllowDrop = true;
            _tabBarTable.UIHandler = this;
        }

        public IPoderosaDocument[] GetHostedDocuments() {
            return KeysToDocuments(_tabBarTable.GetAllDocuments());
        }

        #region IDocumentTabFeature
        public IPoderosaDocument ActiveDocument {
            get {
                return KeyToDocument(_tabBarTable.ActiveTabKey);
            }
        }
        public IPoderosaDocument GetAtOrNull(int index) {
            return KeyToDocument(_tabBarTable.GetAtOrNull(index));
        }
        public int IndexOf(IPoderosaDocument document) {
            return _tabBarTable.IndexOf(DocumentToKey(document));
        }
        public int DocumentCount {
            get {
                return _tabBarTable.TabCount;
            }
        }

        public void Add(IPoderosaDocument document) {
            _tabBarTable.AddTab(DocumentToKey(document));
        }

        public void Remove(IPoderosaDocument document) {
            _tabBarTable.RemoveTab(DocumentToKey(document), false);
        }

        public void Update(IPoderosaDocument document) {
            if (_tabBarTable.InvokeRequired) {
                _tabBarTable.Invoke(new UpdateDelegate(Update), document);
                return;
            }

            _tabBarTable.UpdateDescription(DocumentToKey(document));

            //イベントだけ通知すればいいのでちょっと過剰な処理だが
            if (document == this.ActiveDocument)
                SessionManagerPlugin.Instance.ActivateDocument(document, ActivateReason.InternalAction);
        }
        private delegate void UpdateDelegate(IPoderosaDocument document);

        //SessionManagerからのみ呼ぶこと
        public void Activate(IPoderosaDocument document) {
#if DEBUG
            Debug.Assert(document == null || _tabBarTable.ContainsKey(DocumentToKey(document)));
#endif
            if (document == KeyToDocument(_tabBarTable.ActiveTabKey))
                return; //do nothing

            if (document == null)
                _tabBarTable.Deactivate(false);
            else
                _tabBarTable.Activate(DocumentToKey(document), false);


        }

        public int TabRowCount {
            get {
                return _tabBarTable.TabBarCount; //Controls.Countにすると、終了時にpreferenceに記録することがうまくできない
            }
        }
        public void SetTabRowCount(int count) {
            _tabBarTable.SetTabBarCount(count);
        }

        public void ActivateNextTab() {
            _tabBarTable.ActivateNextTab(true);
        }

        public void ActivatePrevTab() {
            _tabBarTable.ActivatePrevTab(true);
        }

        public IAdaptable GetAdapter(Type adapter) {
            return WindowManagerPlugin.Instance.PoderosaWorld.AdapterManager.GetAdapter(this, adapter);
        }

        #endregion

        #region TabBarTable.IUIHandler
        public void ActivateTab(TabKey key) {
            SessionManagerPlugin.Instance.ActivateDocument(KeyToDocument(key), ActivateReason.TabClick);
        }
        public void MouseMiddleButton(TabKey key) {
            //IPoderosaDocument doc = KeyToDocument(key);
            //SessionManagerPlugin sm = SessionManagerPlugin.Instance;

            //bool was_active = _tabBarTable.ActiveTabKey == key;
            //IPoderosaView view = sm.FindDocumentHost(doc).LastAttachedView;
            //sm.CloseDocument(doc);

            ////アクティブなやつを閉じたらば
            //if (was_active && view != null && view.Document != null) {
            //    sm.ActivateDocument(view.Document, ActivateReason.InternalAction);
            //}
        }
        public void MouseRightButton(TabKey key) {
            IPoderosaDocument doc = KeyToDocument(key);
            IPoderosaContextMenuPoint ctx_pt = (IPoderosaContextMenuPoint)doc.GetAdapter(typeof(IPoderosaContextMenuPoint));

            //メニューが取れない場合は無視
            if (ctx_pt == null || ctx_pt.ContextMenu == null || ctx_pt.ContextMenu.Length == 0)
                return;

            IPoderosaForm f = (IPoderosaForm)_tabBarTable.ParentForm;
            f.ShowContextMenu(ctx_pt.ContextMenu, doc, Control.MousePosition, ContextMenuFlags.None);
        }
        public void StartTabDrag(TabKey key) {
            WindowManagerPlugin.Instance.SetDraggingTabBar(key);
        }
        public void AllocateTabToControl(TabKey key, Control target) {
            IAdaptable ad = target as IAdaptable;
            if (ad == null)
                return;

            IPoderosaView view = (IPoderosaView)ad.GetAdapter(typeof(IPoderosaView));
            if (view == null)
                return;

            SessionManagerPlugin.Instance.AttachDocumentAndView(KeyToDocument(key), view);
        }
        public void BypassDragEnter(DragEventArgs args) {
            try {
                WindowManagerPlugin.Instance.BypassDragEnter(_tabBarTable.ParentForm, args);
            }
            catch (Exception ex) {
                RuntimeUtil.ReportException(ex);
            }
        }
        public void BypassDragDrop(DragEventArgs args) {
            try {
                WindowManagerPlugin.Instance.BypassDragDrop(_tabBarTable.ParentForm, args);
            }
            catch (Exception ex) {
                RuntimeUtil.ReportException(ex);
            }
        }
        #endregion


        private static IPoderosaDocument KeyToDocument(TabKey key) {
            if (key == null)
                return null;
            Debug.Assert(key is InternalTabKey);
            return ((InternalTabKey)key).PoderosaDocument;
        }
        private static TabKey DocumentToKey(IPoderosaDocument doc) {
            return new InternalTabKey(doc);
        }
        private static IPoderosaDocument[] KeysToDocuments(TabKey[] keys) {
            IPoderosaDocument[] r = new IPoderosaDocument[keys.Length];
            for (int i = 0; i < r.Length; i++)
                r[i] = KeyToDocument(keys[i]);
            return r;
        }

        //TabKey
        public class InternalTabKey : TabKey {
            private IPoderosaDocument _document;
            public InternalTabKey(IPoderosaDocument doc)
                : base(doc) {
                _document = doc;
            }
            public override string Caption {
                get {
                    return _document.Caption;
                }
            }
            public override Image Icon {
                get {
                    return _document.Icon;
                }
            }

            public IPoderosaDocument PoderosaDocument {
                get {
                    return _document;
                }
            }
        }
    }

    internal class CommandShortcutKeyHandler : IKeyHandler {
        private PoderosaForm _window;

        public CommandShortcutKeyHandler(PoderosaForm window) {
            _window = window;
        }

        public UIHandleResult OnKeyProcess(Keys key) {
            IGeneralCommand cmd = CommandManagerPlugin.Instance.Find(key);
            if (cmd != null) {
                try {
                    if (cmd.CanExecute(_window))
                        CommandManagerPlugin.Instance.Execute(cmd, _window);
                    return UIHandleResult.Stop; //キーが割り当てられていれば実行ができるかどうかにかかわらずStop。でないとAltキーがらみのときメニューにフォーカスが奪われてしまう
                }
                catch (Exception ex) {
                    RuntimeUtil.ReportException(ex);
                }
            }
            return UIHandleResult.Pass;
        }

        public string Name {
            get {
                return "shortcut-key";
            }
        }
    }

    //Alt+<n>, Ctrl+Tabなど、カスタマイズ不可の動作を扱う
    internal class FixedShortcutKeyHandler : IKeyHandler {
        private MainWindow _window;

        public FixedShortcutKeyHandler(MainWindow window) {
            _window = window;
        }

        public UIHandleResult OnKeyProcess(Keys key) {
            Keys modifier = key & Keys.Modifiers;
            Keys body = key & Keys.KeyCode;
            int n = (int)body - (int)Keys.D1;
            if (modifier == Keys.Alt && n >= 0 && n <= 8) { //１から９のキーが#0から#8までに対応する
                IPoderosaDocument doc = _window.DocumentTabFeature.GetAtOrNull(n);
                if (doc != null) {
                    SessionManagerPlugin.Instance.ActivateDocument(doc, ActivateReason.InternalAction);
                    return UIHandleResult.Stop;
                }
            }
            else if (body == Keys.Tab && (modifier == Keys.Control || modifier == (Keys.Control | Keys.Shift))) { //Ctrl+Tab, Ctrl+Shift+Tab
                IPoderosaDocument doc = _window.DocumentTabFeature.ActiveDocument;
                //ドキュメントはあるがアクティブなやつはない、という状態もある
                int count = _window.DocumentTabFeature.DocumentCount;
                if (count > 0) {
                    int index = doc == null ? -1 : _window.DocumentTabFeature.IndexOf(doc); //docがnullのときは別扱い

                    if (modifier == Keys.Control)
                        index = (doc == null || index == count - 1) ? 0 : index + 1;
                    else
                        index = (doc == null || index == 0) ? count - 1 : index - 1;

                    SessionManagerPlugin.Instance.ActivateDocument(_window.DocumentTabFeature.GetAtOrNull(index), ActivateReason.InternalAction);
                    return UIHandleResult.Stop;
                }
            }

            return UIHandleResult.Pass;
        }

        public string Name {
            get {
                return "fixed-key";
            }
        }
    }

    internal static class BuildTimeStampUtil
    {
        public static DateTime StrToDateTime(string str, bool toUtc = false, bool emptyToZeroDateTime = false)
        {
            if (string.IsNullOrEmpty(str))
            {
                if (emptyToZeroDateTime) return new DateTime(0);
                return new DateTime(0);
            }
            DateTime ret = new DateTime(0);

            str = str.Trim();
            string[] sps =
                {
                    " ",
                    "_",
                    "　",
                    "\t",
                    "T",
                };

            string[] tokens = str.Split(sps, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length != 2)
            {
                int r1 = str.IndexOf("年", StringComparison.OrdinalIgnoreCase);
                int r2 = str.IndexOf("月", StringComparison.OrdinalIgnoreCase);
                int r3 = str.IndexOf("日", StringComparison.OrdinalIgnoreCase);

                if (r1 != -1 && r2 != -1 && r3 != -1)
                {
                    tokens = new string[2];

                    tokens[0] = str.Substring(0, r3 + 1);
                    tokens[1] = str.Substring(r3 + 1);
                }
            }

            if (tokens.Length == 2)
            {
                DateTime dt1 = StrToDate(tokens[0]);
                DateTime dt2 = StrToTime(tokens[1]);

                ret = dt1.Date + dt2.TimeOfDay;
            }
            else if (tokens.Length == 1)
            {
                if (tokens[0].Length == 14)
                {
                    // yyyymmddhhmmss
                    DateTime dt1 = StrToDate(tokens[0].Substring(0, 8));
                    DateTime dt2 = StrToTime(tokens[0].Substring(8));

                    ret = dt1.Date + dt2.TimeOfDay;
                }
                else if (tokens[0].Length == 12)
                {
                    // yymmddhhmmss
                    DateTime dt1 = StrToDate(tokens[0].Substring(0, 6));
                    DateTime dt2 = StrToTime(tokens[0].Substring(6));

                    ret = dt1.Date + dt2.TimeOfDay;
                }
                else
                {
                    // 日付のみ
                    DateTime dt1 = StrToDate(tokens[0]);

                    ret = dt1.Date;
                }
            }
            else
            {
                throw new ArgumentException(str);
            }

            if (toUtc) ret = ret.ToUniversalTime();

            return ret;
        }

        // 文字列を int 型に変換する
        public static int StrToInt(string str)
        {
            try
            {
                str = str.Trim();
                str = str.Replace(",", "");
                if (int.TryParse(str, out int ret))
                {
                    return ret;
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        public static bool IsNumber(string str)
        {
            str = str.Trim();
            str = str.Replace(",", "");

            if (string.IsNullOrEmpty(str)) return false;

            foreach (char c in str)
            {
                if (IsNumber(c) == false)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsNumber(char c)
        {
            if (c >= '0' && c <= '9')
            {
            }
            else if (c == '-')
            {
            }
            else
            {
                return false;
            }

            return true;
        }

        public static DateTime StrToDate(string str, bool toUtc = false, bool emptyToZeroDateTime = false)
        {
            if (emptyToZeroDateTime && string.IsNullOrEmpty(str)) return new DateTime(0);

            string[] sps =
                {
                    "/",
                    "/",
                    "-",
                    ":",
                    "年",
                    "月",
                    "日",
                };
            str = str.Trim();
            //Str.NormalizeString(ref str, true, true, false, false);

            string[] youbi =
            {
                "月", "火", "水", "木", "金", "土", "日",
            };

            foreach (string ys in youbi)
            {
                string ys2 = string.Format("({0})", ys);

                str = str.Replace(ys2, "");
            }

            string[] tokens;

            DateTime ret = new DateTime(0);

            tokens = str.Split(sps, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 3)
            {
                // yyyy/mm/dd
                string yearStr = tokens[0];
                string monthStr = tokens[1];
                string dayStr = tokens[2];
                int year = 0;
                int month = 0;
                int day = 0;

                if ((yearStr.Length == 1 || yearStr.Length == 2) && IsNumber(yearStr))
                {
                    year = 2000 + StrToInt(yearStr);
                }
                else if (yearStr.Length == 4 && IsNumber(yearStr))
                {
                    year = StrToInt(yearStr);
                }

                if ((monthStr.Length == 1 || monthStr.Length == 2) && IsNumber(monthStr))
                {
                    month = StrToInt(monthStr);
                }
                if ((dayStr.Length == 1 || dayStr.Length == 2) && IsNumber(dayStr))
                {
                    day = StrToInt(dayStr);
                }

                if (year < 1800 || year > 9000 || month <= 0 || month >= 13 || day <= 0 || day >= 32)
                {
                    throw new ArgumentException(str);
                }

                ret = new DateTime(year, month, day);
            }
            else if (tokens.Length == 1)
            {
                if (str.Length == 8)
                {
                    // yyyymmdd
                    string yearStr = str.Substring(0, 4);
                    string monthStr = str.Substring(4, 2);
                    string dayStr = str.Substring(6, 2);
                    int year = int.Parse(yearStr);
                    int month = int.Parse(monthStr);
                    int day = int.Parse(dayStr);

                    if (year < 1800 || year > 9000 || month <= 0 || month >= 13 || day <= 0 || day >= 32)
                    {
                        throw new ArgumentException(str);
                    }

                    ret = new DateTime(year, month, day);
                }
                else if (str.Length == 6)
                {
                    // yymmdd
                    string yearStr = str.Substring(0, 2);
                    string monthStr = str.Substring(2, 2);
                    string dayStr = str.Substring(4, 2);
                    int year = int.Parse(yearStr) + 2000;
                    int month = int.Parse(monthStr);
                    int day = int.Parse(dayStr);

                    if (year < 1800 || year > 9000 || month <= 0 || month >= 13 || day <= 0 || day >= 32)
                    {
                        throw new ArgumentException(str);
                    }

                    ret = new DateTime(year, month, day);
                }
            }
            else
            {
                throw new ArgumentException(str);
            }

            if (toUtc)
            {
                ret = ret.ToUniversalTime();
            }

            return ret;
        }


        public static DateTime StrToTime(string str, bool toUtc = false, bool emptyToZeroDateTime = false)
        {
            if (emptyToZeroDateTime && string.IsNullOrEmpty(str)) return new DateTime(0);

            DateTime ret = new DateTime(0);

            string[] sps =
                {
                    "/",
                    "-",
                    ":",
                    "時",
                    "分",
                    "秒",
                };
            
            str = str.Trim();

            string[] tokens;

            tokens = str.Split(sps, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 3)
            {
                // hh:mm:ss
                string hourStr = tokens[0];
                string minuteStr = tokens[1];
                string secondStr = tokens[2];
                string msecStr = "";
                int hour = -1;
                int minute = -1;
                int second = -1;
                int msecond = 0;
                long add_ticks = 0;

                int msec_index = secondStr.IndexOf(".");
                if (msec_index != -1)
                {
                    msecStr = secondStr.Substring(msec_index + 1);
                    secondStr = secondStr.Substring(0, msec_index);

                    msecStr = "0." + msecStr;

                    decimal tmp = decimal.Parse(msecStr);
                    msecond = (int)((tmp % 1.0m) * 1000.0m);
                    add_ticks = (int)((tmp % 0.001m) * 10000000.0m);
                }

                if ((hourStr.Length == 1 || hourStr.Length == 2) && IsNumber(hourStr))
                {
                    hour = StrToInt(hourStr);
                }
                if ((minuteStr.Length == 1 || minuteStr.Length == 2) && IsNumber(minuteStr))
                {
                    minute = StrToInt(minuteStr);
                }
                if ((secondStr.Length == 1 || secondStr.Length == 2) && IsNumber(secondStr))
                {
                    second = StrToInt(secondStr);
                }

                if (hour < 0 || hour >= 25 || minute < 0 || minute >= 60 || second < 0 || second >= 60 || msecond < 0 || msecond >= 1000)
                {
                    throw new ArgumentException(str);
                }

                ret = new DateTime(2000, 1, 1, hour, minute, second, msecond).AddTicks(add_ticks);
            }
            else if (tokens.Length == 2)
            {
                // hh:mm
                string hourStr = tokens[0];
                string minuteStr = tokens[1];
                int hour = -1;
                int minute = -1;
                int second = 0;

                if ((hourStr.Length == 1 || hourStr.Length == 2) && IsNumber(hourStr))
                {
                    hour = StrToInt(hourStr);
                }
                if ((minuteStr.Length == 1 || minuteStr.Length == 2) && IsNumber(minuteStr))
                {
                    minute = StrToInt(minuteStr);
                }

                if (hour < 0 || hour >= 25 || minute < 0 || minute >= 60 || second < 0 || second >= 60)
                {
                    throw new ArgumentException(str);
                }

                ret = new DateTime(2000, 1, 1, hour, minute, second);
            }
            else if (tokens.Length == 1)
            {
                string hourStr = tokens[0];
                int hour = -1;
                int minute = 0;
                int second = 0;
                int msec = 0;

                if ((hourStr.Length == 1 || hourStr.Length == 2) && IsNumber(hourStr))
                {
                    // hh
                    hour = StrToInt(hourStr);
                }
                else
                {
                    if ((hourStr.Length == 4) && IsNumber(hourStr))
                    {
                        // hhmm
                        int i = StrToInt(hourStr);
                        hour = i / 100;
                        minute = i % 100;
                    }
                    else if ((hourStr.Length == 6) && IsNumber(hourStr))
                    {
                        // hhmmss
                        int i = StrToInt(hourStr);
                        hour = i / 10000;
                        minute = ((i % 10000) / 100);
                        second = i % 100;
                    }
                    else if ((hourStr.Length == 10 && hourStr[6] == '.'))
                    {
                        // hhmmss.abc
                        int i = StrToInt(hourStr.Substring(0, 6));
                        hour = i / 10000;
                        minute = ((i % 10000) / 100);
                        second = i % 100;

                        msec = StrToInt(hourStr.Substring(7));
                    }
                }

                if (hour < 0 || hour >= 25 || minute < 0 || minute >= 60 || second < 0 || second >= 60 || msec < 0 || msec >= 1000)
                {
                    throw new ArgumentException(str);
                }

                ret = new DateTime(2000, 1, 1, hour, minute, second, msec);
            }
            else
            {
                throw new ArgumentException(str);
            }

            if (toUtc)
            {
                ret = ret.ToUniversalTime();
            }

            return ret;
        }

        public static DateTime GetLinkerTimestampUtc(Assembly assembly)
        {
            var location = assembly.Location;
            return GetLinkerTimestampUtc(location);
        }

        public static DateTime GetLinkerTimestampUtc(string filePath)
        {
            const int peHeaderOffset = 60;
            const int linkerTimestampOffset = 8;
            var bytes = new byte[2048];

            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file.Read(bytes, 0, bytes.Length);
            }

            var headerPos = BitConverter.ToInt32(bytes, peHeaderOffset);
            var secondsSince1970 = BitConverter.ToInt32(bytes, headerPos + linkerTimestampOffset);
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return dt.AddSeconds(secondsSince1970);
        }

        static DateTime GetThisAssemblyBuildDateInternal()
        {
            return GetLinkerTimestampUtc(typeof(BuildTimeStampUtil).Assembly);
        }

        static DateTime? cached = null;

        public static DateTime GetThisAssemblyBuildDate()
        {
            if (cached == null)
            {
                cached = GetThisAssemblyBuildDateInternal();
            }

            return cached.Value;
        }
    }

    internal class WindowCaptionManager : IActiveDocumentChangeListener {

        public static string GetTimeStampStr()
        {
            var dt = BuildTimeStampUtil.GetThisAssemblyBuildDate();

            return string.Format("{0:D4}.{1:D2}", dt.Year, dt.Month);
        }

        public void OnDocumentActivated(IPoderosaMainWindow window, IPoderosaDocument document) {
            window.AsForm().Text = String.Format("{0} - Poderosa {1}", document.Caption, GetTimeStampStr());
        }

        public void OnDocumentDeactivated(IPoderosaMainWindow window) {
            window.AsForm().Text = string.Format("Poderosa {0}", GetTimeStampStr());
        }
    }
}
