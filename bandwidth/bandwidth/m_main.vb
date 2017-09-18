Module m_main

    Sub Main()

        'test loop iterations
        Dim nIte As Integer = 10
        'test number of junctions
        Dim n As Integer = 5
        Dim cycl As Integer = 120
        Dim leng(n - 2) As Double
        Dim sped(n - 2) As Double
        Dim trav(n - 2) As Double
        Dim trav2(n - 2) As Double
        Dim gini(n - 1) As Integer
        Dim gini2(n - 1) As Integer
        Dim gend(n - 1) As Integer
        Dim gend2(n - 1) As Integer

        Dim opt As BandMaximiser
        Dim r As New Random

        For i As Integer = 1 To nIte
            For j As Integer = 0 To n - 1
                'travel times
                If j < n - 1 Then
                    trav(j) = r.Next(30, 130)
                    trav2(j) = -trav(j) * 0.8
                End If

                gini(j) = r.Next(0, cycl + 1)
                gend(j) = gini(j) + r.Next(30, 70)
                If gend(j) > cycl Then gend(j) -= cycl

                gini2(j) = gini(j) + r.Next(0, cycl + 1)
                gend2(j) = gend(j) + gini2(j) - gini(j) - r.Next(0, 10)
                If gini2(j) > cycl Then gini2(j) -= cycl
                If gend2(j) > cycl Then gend2(j) -= cycl

            Next

            ReDim corridor(1)
            corridor(1) = New t_CORRIDOR(n)

            'opt = New BandMaximiser(cycl, trav, gini, gend, trav2:=trav2)
            'offs = opt.OneWayOffsets()
            'offs = opt.TwoWayOffsets()
            opt = New BandMaximiser(corridor(1).cycl,
                                    corridor(1).trav, corridor(1).gini, corridor(1).gend,
                                    gini2:=corridor(1).gini2, gend2:=corridor(1).gend2)
            corridor(1).offs = opt.OneWayOffsets.Select(Function(x) CInt(Math.Round(x))).ToArray

            'dummy viewer creation
            Dim view As New CorridorViewForm(1)
            view.Show()
            view.Draw()

            view.Close()
            corridor(1).offs = opt.TwoWayOffsets(1).Select(Function(x) CInt(Math.Round(x))).ToArray
            view = New CorridorViewForm(1)
            view.Show()
            view.Draw()
            view.Close()

            Console.WriteLine("-----------------------------------------------------------------------------------------------------")
            Console.WriteLine("")

        Next


#If DEBUG Then
        Stop
#End If
    End Sub

    'PM DEFINE THIS CLASS IN M_INPUT
    Public Class t_CORRIDOR
#Region "DEFINITIONS"
        ''' <summary>
        ''' Corridor ID
        ''' </summary>
        Public idno As Integer

        ''' <summary>
        ''' Corridor Name
        ''' </summary>
        Public name As String

        ''' <summary>
        ''' Ordered set of junctions
        ''' </summary>
        Public junc As Integer()

        Public distance As Double()

        Public cycl As Integer
        Public offs As Integer()
        Private _gini As Integer()
        Private _gend As Integer()
        Public sped As Double()
        Private _gini2 As Integer()
        Private _gend2 As Integer()
        Public sped2 As Double()

        Public ReadOnly Property njunc As Integer
            Get
                If junc Is Nothing Then
                    Return 0
                Else
                    Return junc.Length
                End If
            End Get
        End Property

#End Region

#Region "CONSTRUCTORS"
        ''' <summary>
        ''' Generate a new random corridor with the specified number of junctions
        ''' </summary>
        ''' <param name="njunc"></param>
        Public Sub New(ByVal njunc As Integer)
            Dim i As Integer

            ReDim Me.junc(njunc - 1)
            ReDim Me.offs(njunc - 1)
            ReDim Me._gini(njunc - 1)
            ReDim Me._gend(njunc - 1)
            ReDim Me._gini2(njunc - 1)
            ReDim Me._gend2(njunc - 1)


            ReDim Me.distance(njunc - 1)
            ReDim Me.sped(njunc - 2)
            ReDim Me.sped2(njunc - 2)

            For i = 0 To njunc - 1
                Me.junc(i) = i + 1
            Next
            Me.idno = 1
            Me.name = "Aorta"
            Me.cycl = 120

            Dim r As New Random
            For i = 0 To njunc - 1
                'LINK PROPERTIES
                If i > 0 Then distance(i) = distance(i - 1) + r.Next(150, 400)
                If i < njunc - 1 Then
                    sped(i) = (60 - r.Next(0, 31)) / 3.6
                    sped2(i) = sped(i)
                End If

                _gini(i) = r.Next(0, cycl + 1)
                _gend(i) = _gini(i) + r.Next(30, 70)
                If _gend(i) > cycl Then _gend(i) -= cycl

                _gini2(i) = _gini(i) + r.Next(0, cycl + 1)
                _gend2(i) = _gend(i) + _gini2(i) - _gini(i) - r.Next(0, 10)
                If _gini2(i) > cycl Then _gini2(i) -= cycl
                If _gend2(i) > cycl Then _gend2(i) -= cycl
            Next

        End Sub

#End Region

#Region "PUBLIC PROPERTIES"
        Public ReadOnly Property trav As Double()
            Get
                Dim travel_time(njunc - 2) As Double
                For i As Integer = 0 To njunc - 2
                    travel_time(i) = (Me.distance(i + 1) - Me.distance(i)) / Me.sped(i)
                Next
                Return travel_time
            End Get
        End Property

        Public ReadOnly Property trav2 As Double()
            Get
                Dim travel_time(njunc - 2) As Double
                For i As Integer = 0 To njunc - 2
                    travel_time(i) = (Me.distance(i) - Me.distance(i + 1)) / Me.sped(i)
                Next
                Return travel_time
            End Get
        End Property

        Public ReadOnly Property gini As Integer()
            Get
                Return Me._gini.Zip(offs, Function(base, offset) base + offset - If(base + offset > cycl, cycl, 0)).ToArray
            End Get
        End Property

        Public ReadOnly Property gend As Integer()
            Get
                Return Me._gend.Zip(offs, Function(base, offset) base + offset - If(base + offset > cycl, cycl, 0)).ToArray
            End Get
        End Property

        Public ReadOnly Property gini2 As Integer()
            Get
                Return Me._gini2.Zip(offs, Function(base, offset) base + offset - If(base + offset > cycl, cycl, 0)).ToArray
            End Get
        End Property

        Public ReadOnly Property gend2 As Integer()
            Get
                Return Me._gend2.Zip(offs, Function(base, offset) base + offset - If(base + offset > cycl, cycl, 0)).ToArray
            End Get
        End Property
#End Region

    End Class
    Public corridor As t_CORRIDOR()

    'PM REM
    Enum VissigSignalState As Integer
        NotUsed = -1
        '----- commands -----
        Red = 1
        Green = 3
        Off = 7
        '--- fixed states ---
        Amber = 4
        RedAmber = 2
        FlashingGreen = 5
        FlashingRed = 8
    End Enum
End Module
