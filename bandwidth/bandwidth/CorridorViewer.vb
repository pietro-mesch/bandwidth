Option Strict On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class CorridorViewer
#Region "DECLARATIONS"
    'Private WithEvents _theViewer As TDE_Viewer = TDE_Viewer.Itself
    Private WithEvents _plotBox As PictureBox

    Private _backgroundBitmap As Bitmap
    Private _g As Graphics
    Private _lockedForPlotting As Boolean

    'Private _theJunction As t_JUNC
    'Private _theProgram As t_SPRG
    Private _theCorridor As t_CORRIDOR

    'Private _theDynamicProgram As t_SPRG
    Private _dynamicProgramFound As Boolean
    Private _dynamicProgramRequested As Boolean
    Private _selectedSgrp As Integer

    'row bounding boxes
    Private _juncBoxes As RectangleF()

    Private _currDateTime As Date

    Public Sub New(ByVal corridorIndex As Integer, ByRef pictureBox As PictureBox)
        'setCurrentDatetime(CInt(resInst(_theViewer.SelectedIntervalIndex)))
        _theCorridor = corridor(corridorIndex)
        _plotBox = pictureBox
        SetPlotBoxSize()
        GREEN_BAND_BRUSH = SubdueBrush(Brushes.DarkGreen, 0.5 / (_theCorridor.njunc - 1))
        'GetActiveSignalProgram()

    End Sub
#End Region

#Region "STYLE CONSTANTS"
    Private Const ROW_HEIGHT As Integer = 20
    Private Const ROW_SPACING As Integer = 50
    Private Const TIME_AXIS_HEIGHT As Integer = 40
    Private INNER_PADDING As New Padding(5, 5, 15, 5)
    Private Const COLUMN_NAMES_WIDTH As Integer = 50
    Private Const PIXELS_PER_METRE As Double = 0.4

#Region "COLOURS"
    Private GREEN_BAR_BRUSH As Brush = Brushes.DarkGreen
    Private AMBER_BAR_BRUSH As Brush = Brushes.Gold
    Private RED_BAR_BRUSH As Brush = Brushes.Red
    Private GREEN_BAND_BRUSH As Brush

#End Region

#Region "FONTS"
    Private ReadOnly Property TIME_AXIS_FONT As Font
        Get
            Dim f As New Font("Arial", 9, FontStyle.Regular)
            Return f
        End Get
    End Property

    Private ReadOnly Property HEADER_FONT As Font
        Get
            Return New Font("Arial", 12, FontStyle.Regular)
        End Get
    End Property

#End Region

#Region "PENS"
    Private ReadOnly Property SGRP_AXIS_PEN As Pen
        Get
            Dim p As New Pen(Color.Black, 1)
            p.CustomStartCap = FLAT_LINE_CAP
            p.CustomEndCap = FLAT_LINE_CAP
            Return p
        End Get
    End Property

    Private ReadOnly Property TIME_AXIS_PEN As Pen
        Get
            Dim p As New Pen(Color.Black, 1)
            p.CustomStartCap = FLAT_LINE_CAP
            p.CustomEndCap = ARROW_LINE_CAP
            Return p
        End Get
    End Property

    Private ReadOnly Property SGRP_SELECTION_BRUSH As Brush
        Get
            Dim p As New SolidBrush(Color.FromArgb(30, 30, 30, 30))
            Return p
        End Get
    End Property

    Private ReadOnly Property ARROW_LINE_CAP As Drawing2D.CustomLineCap
        Get
            Dim c As New Drawing2D.AdjustableArrowCap(3, 4)
            Return c
        End Get
    End Property

    Private ReadOnly Property FLAT_LINE_CAP As Drawing2D.CustomLineCap
        Get
            Dim cap_path As New Drawing2D.GraphicsPath
            cap_path.AddLine(3, 0, -3, 0)
            Dim c As New Drawing2D.CustomLineCap(Nothing, cap_path)
            Return c
        End Get
    End Property

#End Region

    Private Enum TickType
        Minimum = 0
        Medium = 1
        Main = 2
    End Enum

    Private Function MIN_TICK_SPACING(tt As TickType) As Integer
        Select Case tt
            Case TickType.Minimum
                Return 4
            Case TickType.Medium
                Return 10
            Case TickType.Main
                Return 40
            Case Else
                Return 0
        End Select
    End Function

    Private Function TICK_PEN(tt As TickType) As Pen
        Select Case tt
            Case TickType.Minimum
                Return New Pen(Color.FromArgb(32, 192, 192, 192), 1)
            Case TickType.Medium
                Return New Pen(Color.FromArgb(64, 128, 128, 128), 1)
            Case TickType.Main
                Return New Pen(Color.FromArgb(64, 128, 128, 128), 2)
            Case Else
                Return Nothing
        End Select
    End Function
#End Region

#Region "PUBLIC METHODS"
    Public Sub Draw()
        '1: initialise drawing and redraw background only if it was reset (e.g. because _theProgram changed)
        InitPlotBox()
        '2: plot the dynamic program
        PlotJunctionPrograms()
        '3: plot selection
        'PlotSelectionBoxes()
        '4:finalise drawing
        FinalizePlotBox()
    End Sub
#End Region

#Region "PRIVATE METHODS"
    ''' <summary>
    ''' Determine the plotBox size based on design parameters and parent dimensions
    ''' </summary>
    Private Sub SetPlotBoxSize()
        'count the rows
        Dim row_count As Integer
        row_count = _theCorridor.njunc

        _plotBox.Height = row_count * ROW_HEIGHT + CInt(_theCorridor.distance(row_count - 1) * PIXELS_PER_METRE) +
                +TIME_AXIS_HEIGHT +
                +INNER_PADDING.Top + INNER_PADDING.Bottom

        ReDim _juncBoxes(row_count)
        For i As Integer = 1 To row_count
            _juncBoxes(i) = New RectangleF(New Point(0, (ROW_HEIGHT + ROW_SPACING) * (i - 1) + INNER_PADDING.Top), New Size(_plotBox.Width, ROW_HEIGHT))
        Next

    End Sub
    'PM REM
    'Private Function Sum(ByVal values As Double(), ByVal from_index As Integer, ByVal to_index As Integer) As Double

    '    Dim tot As Double = 0

    '    If from_index >= 0 AndAlso from_index <= values.Length - 1 _
    '        AndAlso to_index >= 0 AndAlso to_index <= values.Length - 1 Then

    '        For i As Integer = from_index To to_index
    '            tot += values(i)
    '        Next

    '    Else
    '        Throw New ArgumentOutOfRangeException
    '    End If

    '    Return tot

    'End Function

    ''' <summary>
    ''' initialise the plot area and graphics handle
    ''' </summary>
    Private Sub InitPlotBox()
        'first wait for any other drawing operations to complete
        Do While _lockedForPlotting
            System.Threading.Thread.Sleep(100)
        Loop
        'then lock drawing
        _lockedForPlotting = True

        'if the background bitmap is empty
        If _backgroundBitmap Is Nothing Then
            'create background bitmap and draw on it
            _backgroundBitmap = New Bitmap(_plotBox.Width, _plotBox.Height)
            _backgroundBitmap.SetResolution(96, 96)
            _g = InitGraphicsFromImage(_backgroundBitmap)
            DrawBackground()
            'PM PLOT SOMETHING
            'PlotBaseProgram()
            _g.Dispose()
        End If

        'scrap the foreground and set the background to the plot area
        _plotBox.Image = New Bitmap(_backgroundBitmap)
        RefreshPlotBox()
        'move the handle to the plotbox image which is now the same as the background
        _g = InitGraphicsFromImage(_plotBox.Image)
    End Sub

    Private Function InitGraphicsFromImage(i As Image) As Graphics
        Dim gg As Graphics = Graphics.FromImage(i)
        gg.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        gg.TextRenderingHint = Text.TextRenderingHint.AntiAliasGridFit
        gg.CompositingQuality = Drawing2D.CompositingQuality.HighQuality
        Return gg
    End Function

    ''' <summary>
    '''  refresh the plot area and dispose of the graphics handle
    ''' </summary>
    Private Sub FinalizePlotBox()
        RefreshPlotBox()
        _g.Dispose()

        'release lock
        _lockedForPlotting = False
    End Sub

    Private Sub ResetPlotBox()
        'first wait for drawing operations to complete before attempting to delete the background
        Do While _lockedForPlotting
            System.Threading.Thread.Sleep(100)
        Loop
        _backgroundBitmap = Nothing
    End Sub

    Private Sub RefreshPlotBox()
        If Not _plotBox.InvokeRequired Then
            _plotBox.Refresh()
        Else
            _plotBox.BeginInvoke(New RefreshDelegate(AddressOf _plotBox.Refresh))
        End If
    End Sub
    Private Delegate Sub RefreshDelegate()

    ''' <summary>
    ''' Draws the background of the Signal Program Plot
    ''' </summary>
    Private Sub DrawBackground()
        For i As Integer = 0 To _theCorridor.njunc - 1

            PlotTimeAxisAndTicks()

            'plot the label with sgrp idno and name
            PlotJunctionLabel(i)

            'plot the signal group axis
            PlotSgrpAxis(i)

        Next
    End Sub

    ''' <summary>
    ''' plot the label with sgrp idno and name
    ''' </summary>
    ''' <param name="rowNumber">the row being plotted</param>
    Private Sub PlotJunctionLabel(ByVal rowNumber As Integer)
        Dim format As New StringFormat
        format.LineAlignment = StringAlignment.Center
        format.Alignment = StringAlignment.Center
        'calculate position
        Dim yPos As Integer = GetBaselineVerticalPosition(rowNumber)
        'write label with group idno and name
        Dim labelRectangle As New RectangleF(New Point(INNER_PADDING.Left, yPos), New Size(_plotBox.Width - INNER_PADDING.Right, ROW_HEIGHT))
        _g.DrawString(rowNumber.ToString,
                     HEADER_FONT,
                     Brushes.Black,
                     labelRectangle, format)
    End Sub

    ''' <summary>
    ''' draw the line along which signal state bars are plotted
    ''' </summary>
    ''' <param name="rowNumber">the row being plotted</param>
    Private Sub PlotSgrpAxis(ByVal rowNumber As Integer)
        'calculate position
        Dim yPos As Integer = GetBaselineVerticalPosition(rowNumber)

        'endpoints
        Dim A As New Point(INNER_PADDING.Left, yPos)
        Dim B As New Point(_plotBox.Width - INNER_PADDING.Right, yPos)

        'draw axis
        _g.DrawLine(SGRP_AXIS_PEN, A, B)
    End Sub

    Private Sub PlotTimeAxisAndTicks()
        Dim format As New StringFormat
        format.LineAlignment = StringAlignment.Near
        format.Alignment = StringAlignment.Center

        'calculate position
        Dim yPos As Integer = _plotBox.Height - INNER_PADDING.Bottom - CInt(TIME_AXIS_HEIGHT / 3)

        'write axis label
        Dim labelRectangle As New RectangleF(New Point(INNER_PADDING.Left, yPos),
                                             New Size(_plotBox.Width - INNER_PADDING.Right, TIME_AXIS_HEIGHT))
        _g.DrawString("Program Cycle [s]",
                     TIME_AXIS_FONT,
                     Brushes.Black,
                     labelRectangle, format)


        'draw axis
        yPos = _plotBox.Height - INNER_PADDING.Bottom - CInt(TIME_AXIS_HEIGHT * 3 / 4)
        Dim A As New Point(INNER_PADDING.Left, yPos)
        Dim B As New Point(_plotBox.Width - INNER_PADDING.Right, yPos)
        _g.DrawLine(TIME_AXIS_PEN, A, B)

        'draw ticks
        format.LineAlignment = StringAlignment.Near
        format.Alignment = StringAlignment.Center
        Dim tt As TickType = TickType.Minimum
        For Each k As Integer In {1, 5, 10, 20, 30, 60}
            If (B.X - A.X) * k / _theCorridor.cycl > MIN_TICK_SPACING(tt) Then
                For t As Integer = 0 To _theCorridor.cycl Step k
                    _g.DrawLine(TICK_PEN(tt),
                            New Point(CInt(A.X + (B.X - A.X) * t / _theCorridor.cycl), yPos - CInt(TIME_AXIS_HEIGHT / 4)),
                            New Point(CInt(A.X + (B.X - A.X) * t / _theCorridor.cycl), INNER_PADDING.Top))
                    If tt > TickType.Minimum Then
                        _g.DrawLine(TICK_PEN(tt),
                           New Point(CInt(A.X + (B.X - A.X) * t / _theCorridor.cycl), yPos - 2),
                           New Point(CInt(A.X + (B.X - A.X) * t / _theCorridor.cycl), yPos + 2))

                        If tt = TickType.Main Then
                            labelRectangle = New RectangleF(New Point(CInt(A.X + (B.X - A.X) * t / _theCorridor.cycl - MIN_TICK_SPACING(tt) / 2), yPos + 2),
                                             New Size(MIN_TICK_SPACING(tt), CInt(TIME_AXIS_HEIGHT / 2)))
                            _g.DrawString(String.Format("{0}", t),
                                         TIME_AXIS_FONT,
                                         Brushes.Black,
                                         labelRectangle, format)
                        End If
                    End If
                Next

                If tt < TickType.Main Then
                    tt = DirectCast(tt + 1, TickType)
                Else
                    Exit For
                End If
            End If
        Next


    End Sub

    ''' <summary>
    ''' plots the through phases at each junction
    ''' </summary>
    Private Sub PlotJunctionPrograms()

        For i As Integer = 0 To 0
            'For i As Integer = 0 To _theCorridor.njunc - 1
            PlotGreenBands(i)
            PlotThroughPhases(i)
        Next

    End Sub

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="junc_internal_index"></param>
    Private Sub PlotThroughPhases(ByVal junc_internal_index As Integer)

        PlotStatusBar(junc_internal_index, _theCorridor.gini(junc_internal_index), _theCorridor.gend(junc_internal_index), VissigSignalState.Green, False)
        PlotStatusBar(junc_internal_index, _theCorridor.gini2(junc_internal_index), _theCorridor.gend2(junc_internal_index), VissigSignalState.Green, True)

    End Sub

    Private Sub PlotGreenBands(ByVal junc_internal_index As Integer)
        'forward
        Dim from_start, from_end, to_start, to_end As Double
        'initialise forward band limits
        from_start = _theCorridor.gini(junc_internal_index)
        from_end = _theCorridor.gend(junc_internal_index)
        If from_start > from_end Then from_end += _theCorridor.cycl

        For i As Integer = junc_internal_index + 1 To _theCorridor.njunc - 2
            'endpoints
            Dim A As New Point(INNER_PADDING.Left, GetBaselineVerticalPosition(junc_internal_index))
            Dim B As New Point(_plotBox.Width - INNER_PADDING.Right, GetBaselineVerticalPosition(junc_internal_index))
            Dim C As New Point(INNER_PADDING.Left, GetBaselineVerticalPosition(junc_internal_index + 1))
            Dim D As New Point(_plotBox.Width - INNER_PADDING.Right, GetBaselineVerticalPosition(junc_internal_index + 1))

            'initialise backward band limits
            to_start = _theCorridor.gini(junc_internal_index + 1)
            to_end = _theCorridor.gend(junc_internal_index + 1)
            If to_start > to_end Then to_start -= _theCorridor.cycl

            Dim parallelograms As Region() = GetGreenBands(from_start, from_end, to_start, to_end, A, B, C, D, _theCorridor.trav(junc_internal_index))
            _g.FillRegion(GREEN_BAND_BRUSH, parallelograms(0))
        Next
    End Sub

    Private Function GetGreenBands(ByVal from_start As Double, ByVal from_end As Double, ByVal to_start As Double, ByVal to_end As Double,
                                   ByVal A As Point, ByVal B As Point, ByVal C As Point, ByVal D As Point, ByVal travel_time As Double) As Region()

        Dim fwd(1) As Region
        Dim x1 As Integer = CInt(A.X + (B.X - A.X) * from_start / _theCorridor.cycl)
        Dim x2 As Integer = CInt(A.X + (B.X - A.X) * from_end / _theCorridor.cycl)
        Dim x3 As Integer = CInt(x1 + (B.X - A.X) * travel_time / _theCorridor.cycl)
        Dim x4 As Integer = CInt(x2 + (B.X - A.X) * travel_time / _theCorridor.cycl)

        'fwd(0) = New Region(New GraphicsPath({New Point(x1, A.Y), New Point(x2, A.Y), New Point(x4, A.Y), New Point(x3, A.Y)},
        '                                     {0, 1, 1, 128}))
        Dim path As New GraphicsPath
        path.AddPolygon({New Point(x1, A.Y), New Point(x2, A.Y), New Point(x4, C.Y), New Point(x3, C.Y)})
        path.CloseFigure()
        fwd(0) = New Region(path)
        Return fwd

    End Function

    ''' <summary>
    ''' Plots a bar representing the signal group status duration over the program cycle
    ''' </summary>
    ''' <param name="rowNumber"></param>
    ''' <param name="t1"></param>
    ''' <param name="t2"></param>
    ''' <param name="s"></param>
    ''' <param name="isSecondaryDirection"></param>
    Private Sub PlotStatusBar(ByVal rowNumber As Integer,
                              ByVal t1 As Integer, ByVal t2 As Integer, ByVal s As VissigSignalState,
                              ByVal isSecondaryDirection As Boolean)
        Dim thickness As Integer
        Dim brush As Brush
        Select Case s
            Case VissigSignalState.Green
                thickness = ROW_HEIGHT
                brush = GREEN_BAR_BRUSH

            Case VissigSignalState.Amber
                thickness = ROW_HEIGHT
                brush = AMBER_BAR_BRUSH

            Case VissigSignalState.Red
                thickness = CInt(ROW_HEIGHT / 6)
                brush = RED_BAR_BRUSH
            Case Else
                Exit Sub
        End Select

        'calculate position
        Dim yPos As Integer = GetBaselineVerticalPosition(rowNumber)

        'endpoints of the time axis
        Dim A As New Point( INNER_PADDING.Left , yPos)
        Dim B As New Point(_plotBox.Width - INNER_PADDING.Right, yPos)

        Dim rectangles As RectangleF()

        '+1 above the axis, -1 below the axis, 0 both above and below
        Dim position As Integer
        If isSecondaryDirection Then
            position = -1
            brush = SubdueBrush(brush)
        Else
            position = 1
        End If

        'draw
        rectangles = GetRectangles(t1, t2, thickness, _theCorridor.cycl, A, B, position)
        PlotBars(rectangles, brush)
    End Sub

    Private Function GetBaselineVerticalPosition(ByVal row As Integer) As Integer
        Return CInt(Math.Round(INNER_PADDING.Top + row * ROW_HEIGHT + _theCorridor.distance(row) * PIXELS_PER_METRE + CInt(ROW_HEIGHT / 2)))
    End Function

    ''' <summary>
    ''' Returns the rectangles representing the signal status span along the program cycle
    ''' </summary>
    ''' <param name="gini">status onset time</param>
    ''' <param name="gend">status end time</param>
    ''' <param name="thickness">status bar thickness</param>
    ''' <param name="cycle">cycle length</param>
    ''' <param name="a">cycle axis start point</param>
    ''' <param name="b">cycle axis end point</param>
    ''' <param name="position">+1 above the axis, -1 below the axis, 0 both above and below</param>
    ''' <returns></returns>
    Private Function GetRectangles(ByVal gini As Integer, ByVal gend As Integer, ByVal thickness As Integer, ByVal cycle As Integer, ByRef a As Point, ByRef b As Point, ByVal position As Integer) As RectangleF()
        Dim x1 As Single = CSng(a.X + (b.X - a.X) * gini / cycle)
        Dim x2 As Single = CSng(a.X + (b.X - a.X) * gend / cycle)

        'when the rectangle spans over the end of the cycle it must be split
        If x1 > x2 Then
            Return {New RectangleF(x1, CSng(a.Y - If(position <> -1, thickness / 2, 0)), b.X - x1, CSng(thickness * If(position = 0, 1, 0.5))),
                    New RectangleF(a.X, CSng(a.Y - If(position <> -1, thickness / 2, 0)), x2 - a.X, CSng(thickness * If(position = 0, 1, 0.5)))}
        Else
            Return {New RectangleF(x1, CSng(a.Y - If(position <> -1, thickness / 2, 0)), x2 - x1, CSng(thickness * If(position = 0, 1, 0.5)))}
        End If
    End Function

    ''' <summary>
    ''' Plots the given shapes using the brush specified
    ''' </summary>
    ''' <param name="r">An array of Rectangles</param>
    ''' <param name="brush">The brush object to use</param>
    Private Sub PlotBars(ByRef r As RectangleF(), ByRef brush As Brush)
        _g.FillRectangles(brush, r)
    End Sub

    Private Function SubdueBrush(ByVal br As Brush, Optional ByVal alpha As Double = 0.6) As Brush
        Dim c As Color = New Pen(br).Color
        Dim A As Integer = CInt(c.A * alpha)
        Dim R As Integer = c.R
        Dim G As Integer = c.G
        Dim B As Integer = c.B

        Return New SolidBrush(Color.FromArgb(A, R, G, B))
    End Function
#End Region

End Class
