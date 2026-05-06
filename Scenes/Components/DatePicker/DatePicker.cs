using System;
using Godot;

public partial class DatePicker : HBoxContainer
{
    [Signal] public delegate void ValueChangedEventHandler(string isoDate);

    private const int YearMin = 1970;
    private int       _yearMax;

    private DateTime?       _value;
    private Button          _mainButton;
    private PopupPanel      _popup;
    private Button          _monthBtn;
    private Button          _prevBtn;
    private Button          _nextBtn;
    private VBoxContainer   _dayView;
    private VBoxContainer   _pickerView;
    private GridContainer   _dayGrid;
    private GridContainer   _monthGrid;
    private ScrollContainer _yearScroll;
    private VBoxContainer   _yearList;
    private HSeparator      _footerSep;
    private Button          _todayBtn;
    private DateTime        _viewDate;
    private int             _pickerYear;
    private bool            _pickerOpen;

    public DateTime? Value
    {
        get => _value;
        set { _value = value; UpdateButtonText(); }
    }

    public override void _Ready()
    {
        _mainButton = new Button
        {
            SizeFlagsHorizontal     = SizeFlags.ShrinkBegin,
            Alignment               = HorizontalAlignment.Left,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        AddChild(_mainButton);
        _mainButton.Pressed += OpenCalendar;
        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        if (_mainButton == null) return;
        if (_value == null) { _mainButton.Text = "✏ —"; return; }
        var today = DateTime.Today;
        string label = _value.Value.Date == today              ? "Today"
                     : _value.Value.Date == today.AddDays(-1) ? "Yesterday"
                     : _value.Value.ToString("MMM d, yyyy");
        _mainButton.Text = $"✏ {label}";
    }

    private void OpenCalendar()
    {
        _viewDate   = _value?.Date ?? DateTime.Today;
        _pickerOpen = false;
        BuildPopup();
        var pos = _mainButton.GlobalPosition + new Vector2(0, _mainButton.Size.Y);
        _popup.Popup(new Rect2I((int)pos.X, (int)pos.Y, 252, CalcPopupHeight()));
    }

    private void BuildPopup()
    {
        if (_popup != null && IsInstanceValid(_popup))
            _popup.QueueFree();

        _popup = new PopupPanel();

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   6);
        margin.AddThemeConstantOverride("margin_right",  6);
        margin.AddThemeConstantOverride("margin_top",    4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);

        // ── Header: ◀  [Month/Year label]  ▶ ────────────────────────────────
        var header = new HBoxContainer();
        _prevBtn  = new Button { Text = "◀", Flat = true, FocusMode = FocusModeEnum.None };
        _monthBtn = new Button
        {
            SizeFlagsHorizontal     = SizeFlags.ExpandFill,
            Flat                    = true,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        _nextBtn = new Button { Text = "▶", Flat = true, FocusMode = FocusModeEnum.None };
        _prevBtn.Pressed  += OnPrevPressed;
        _nextBtn.Pressed  += OnNextPressed;
        _monthBtn.Pressed += TogglePicker;
        header.AddChild(_prevBtn);
        header.AddChild(_monthBtn);
        header.AddChild(_nextBtn);
        vbox.AddChild(header);

        // ── Day view: day-of-week labels + day grid ───────────────────────────
        _dayView = new VBoxContainer();
        _dayView.AddThemeConstantOverride("separation", 0);

        var dowRow = new HBoxContainer();
        dowRow.AddThemeConstantOverride("separation", 0);
        foreach (var d in new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" })
        {
            var lbl = new Label
            {
                Text                = d,
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize   = new Vector2(32, 0),
            };
            lbl.AddThemeFontSizeOverride("font_size", 10);
            lbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
            dowRow.AddChild(lbl);
        }
        _dayView.AddChild(dowRow);

        _dayGrid = new GridContainer { Columns = 7 };
        _dayGrid.AddThemeConstantOverride("h_separation", 0);
        _dayGrid.AddThemeConstantOverride("v_separation", 0);
        _dayGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _dayView.AddChild(_dayGrid);
        vbox.AddChild(_dayView);

        // ── Picker view: scrollable year list + month grid ────────────────────
        _pickerView = new VBoxContainer();
        _pickerView.AddThemeConstantOverride("separation", 4);
        _pickerView.Visible = false;

        // Year scroll
        _yearScroll = new ScrollContainer();
        _yearScroll.CustomMinimumSize   = new Vector2(0, 120);
        _yearScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _yearList = new VBoxContainer();
        _yearList.AddThemeConstantOverride("separation", 0);
        _yearList.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _yearMax = DateTime.Today.Year + 1;
        for (int y = _yearMax; y >= YearMin; y--)
        {
            int year = y;
            var yBtn = new Button
            {
                Text                = year.ToString(),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode           = FocusModeEnum.None,
                Flat                = true,
                CustomMinimumSize   = new Vector2(0, 28),
            };
            yBtn.Pressed += () => SelectYear(year);
            _yearList.AddChild(yBtn);
        }
        _yearScroll.AddChild(_yearList);
        _pickerView.AddChild(_yearScroll);

        // Month grid
        _monthGrid = new GridContainer { Columns = 3 };
        _monthGrid.AddThemeConstantOverride("h_separation", 2);
        _monthGrid.AddThemeConstantOverride("v_separation", 2);
        _monthGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        for (int m = 1; m <= 12; m++)
        {
            int month = m;
            var mBtn = new Button
            {
                Text                = new DateTime(2000, m, 1).ToString("MMM"),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize   = new Vector2(0, 28),
                FocusMode           = FocusModeEnum.None,
            };
            mBtn.Pressed += () =>
            {
                _viewDate = new DateTime(_pickerYear, month,
                    Math.Min(_viewDate.Day, DateTime.DaysInMonth(_pickerYear, month)));
                ClosePicker();
                RebuildGrid();
            };
            _monthGrid.AddChild(mBtn);
        }
        _pickerView.AddChild(_monthGrid);
        vbox.AddChild(_pickerView);

        // ── Footer ────────────────────────────────────────────────────────────
        _footerSep = new HSeparator();
        vbox.AddChild(_footerSep);
        _todayBtn = new Button
        {
            Text                = "Today",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode           = FocusModeEnum.None,
        };
        _todayBtn.Pressed += () => CommitDate(DateTime.Today);
        vbox.AddChild(_todayBtn);

        margin.AddChild(vbox);
        _popup.AddChild(margin);
        AddChild(_popup);

        RebuildGrid();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnPrevPressed()
    {
        if (_pickerOpen)
        {
            _pickerYear = Math.Max(YearMin, _pickerYear - 1);
            UpdatePickerHighlights();
            CallDeferred(nameof(ScrollToPickerYear));
        }
        else
        {
            _viewDate = _viewDate.AddMonths(-1);
            RebuildGrid();
        }
    }

    private void OnNextPressed()
    {
        if (_pickerOpen)
        {
            _pickerYear = Math.Min(DateTime.Today.Year + 1, _pickerYear + 1);
            UpdatePickerHighlights();
            CallDeferred(nameof(ScrollToPickerYear));
        }
        else
        {
            _viewDate = _viewDate.AddMonths(1);
            RebuildGrid();
        }
    }

    private void TogglePicker()
    {
        if (_pickerOpen) ClosePicker();
        else             OpenPicker();
    }

    private void OpenPicker()
    {
        _pickerOpen          = true;
        _pickerYear          = _viewDate.Year;
        _dayView.Visible     = false;
        _pickerView.Visible  = true;
        _footerSep.Visible   = false;
        _todayBtn.Visible    = false;
        UpdatePickerHighlights();
        ResizePopup();
        CallDeferred(nameof(ScrollToPickerYear));
    }

    private void ClosePicker()
    {
        _pickerOpen          = false;
        _dayView.Visible     = true;
        _pickerView.Visible  = false;
        _footerSep.Visible   = true;
        _todayBtn.Visible    = true;
        RefreshHeaderBtn();
        ResizePopup();
    }

    private void SelectYear(int year)
    {
        _viewDate = new DateTime(year, _viewDate.Month,
            Math.Min(_viewDate.Day, DateTime.DaysInMonth(year, _viewDate.Month)));
        ClosePicker();
        RebuildGrid();
    }

    private void UpdatePickerHighlights()
    {
        _monthBtn.Text = _pickerYear.ToString();

        int selectedIdx = _yearMax - _pickerYear;
        for (int i = 0; i < _yearList.GetChildCount(); i++)
        {
            var btn = _yearList.GetChild<Button>(i);
            if (i == selectedIdx)
                btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
            else
                btn.RemoveThemeColorOverride("font_color");
        }

        for (int i = 0; i < _monthGrid.GetChildCount(); i++)
        {
            var btn = _monthGrid.GetChild<Button>(i);
            if (_pickerYear == _viewDate.Year && i + 1 == _viewDate.Month)
                btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
            else
                btn.RemoveThemeColorOverride("font_color");
        }
    }

    private void ScrollToPickerYear()
    {
        if (_yearScroll == null || !IsInstanceValid(_yearScroll)) return;
        int idx          = _yearMax - _pickerYear;
        int scrollTarget = Math.Max(0, idx * 28 - 46); // centre in the 120px window
        _yearScroll.ScrollVertical = scrollTarget;
    }

    private void RefreshHeaderBtn() =>
        _monthBtn.Text = _viewDate.ToString("MMMM yyyy");

    // ── Day grid ──────────────────────────────────────────────────────────────

    private void RebuildGrid()
    {
        RefreshHeaderBtn();

        foreach (Node child in _dayGrid.GetChildren())
            child.Free();

        var firstDay    = new DateTime(_viewDate.Year, _viewDate.Month, 1);
        int startDow    = (int)firstDay.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(_viewDate.Year, _viewDate.Month);

        for (int i = 0; i < startDow; i++)
        {
            var blank = new Control { CustomMinimumSize = new Vector2(32, 28) };
            blank.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _dayGrid.AddChild(blank);
        }

        var today    = DateTime.Today;
        var selected = _value?.Date;

        for (int d = 1; d <= daysInMonth; d++)
        {
            var date = new DateTime(_viewDate.Year, _viewDate.Month, d);
            var btn  = new Button
            {
                Text                = d.ToString(),
                CustomMinimumSize   = new Vector2(32, 28),
                FocusMode           = FocusModeEnum.None,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            if (date.Date == today)
                btn.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 0.4f));
            if (selected.HasValue && date.Date == selected.Value)
                btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
            btn.Pressed += () => CommitDate(date);
            _dayGrid.AddChild(btn);
        }

        ResizePopup();
    }

    // ── Sizing ────────────────────────────────────────────────────────────────

    private void ResizePopup()
    {
        if (_popup == null || !IsInstanceValid(_popup) || !_popup.Visible) return;
        _popup.Size = new Vector2I((int)_popup.Size.X, CalcPopupHeight());
    }

    private int CalcPopupHeight()
    {
        if (_pickerOpen)
        {
            // margins(8) + header(28) + sep(4) = 40 fixed
            // yearScroll(120) + sep(4) + monthGrid(4×28 + 3×2) = 242
            return 40 + 242;
        }

        var firstDay    = new DateTime(_viewDate.Year, _viewDate.Month, 1);
        int startDow    = (int)firstDay.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(_viewDate.Year, _viewDate.Month);
        int rows        = (int)Math.Ceiling((startDow + daysInMonth) / 7.0);
        // margins(8) + header(28) + 3×sep(12) + hsep(8) + today(28) = 84 fixed
        // dayView: dow(18) + grid(rows×28)
        return 84 + 18 + rows * 28;
    }

    // ── Commit ────────────────────────────────────────────────────────────────

    private void CommitDate(DateTime date)
    {
        _value = date;
        UpdateButtonText();
        EmitSignal(SignalName.ValueChanged, date.ToString("yyyy-MM-dd"));
        if (_popup != null && IsInstanceValid(_popup))
            _popup.Hide();
    }
}
