'PM DEFINE THIS CLASS IN M_INPUT
Imports System.IO

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
                sped(i) = (60 - r.Next(0, 21)) / 3.6
                sped2(i) = sped(i)
            End If

            _gini(i) = 0
            _gend(i) = _gini(i) + r.Next(30, 70)
            If _gend(i) > cycl Then _gend(i) -= cycl

            _gini2(i) = r.Next(0, cycl + 1)
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

    Public Function GreenDuration(ByVal return_direction As Boolean) As Double()
        Dim duration(njunc - 1) As Double
        For j As Integer = 0 To njunc - 1
            If return_direction Then
                duration(j) = Me._gend2(j) - Me._gini2(j)
            Else
                duration(j) = Me._gend(j) - Me._gini(j)
            End If
            If duration(j) < 0 Then duration(j) += Me.cycl
        Next

        Return duration
    End Function

    Public Function AbsoluteOffset(ByVal return_direction As Boolean) As Double()
        Dim midpoint As Double
        Dim offset(njunc - 1) As Double

        Dim the_gini, the_gend As Integer()
        If return_direction Then
            the_gini = Me.gini2
            the_gend = Me.gend2
        Else
            the_gini = Me.gini
            the_gend = Me.gend
        End If

        For j As Integer = 0 To njunc - 1

            'first find green midpoint
            If the_gini(j) < the_gend(j) Then
                midpoint = (the_gini(j) + the_gend(j)) / 2
            Else
                midpoint = (the_gini(j) + the_gend(j) + Me.cycl) / 2
            End If

            'find its distance to the nearest cycle
            offset(j) = modC(midpoint)
        Next

        Return offset
    End Function

    ''' <summary>
    ''' Internal offset i.e. the position of the return green phase wrt the main one
    ''' </summary>
    Public ReadOnly Property InternalOffset() As Double()
        Get
            Dim offset As Double() = Me.AbsoluteOffset(False)
            Dim offset2 As Double() = Me.AbsoluteOffset(True)

            Dim internal(njunc - 1) As Double

            For i As Integer = 0 To njunc - 1
                internal(i) = modC(offset2(i) - offset(i))
            Next

            Return internal

        End Get
    End Property

    ''' <summary>
    ''' Internal offsets mapped to the FoR of the first junction
    ''' </summary>
    Public ReadOnly Property InternalOffsetZero() As Double()
        Get
            Dim offset As Double() = Me.InternalOffset

            Dim internal(njunc - 1) As Double

            For i As Integer = 0 To njunc - 1
                internal(i) = modC(offset(i) + If(i > 0, Sum(Me.trav, 0, i - 1) - Sum(Me.trav2, 0, i - 1), 0))
            Next

            Return internal

        End Get
    End Property
#End Region

#Region "PRIVATE METHODS"
    ''' <summary>
    ''' Returns the distance of t to the nearest integer cycle
    ''' </summary>
    ''' <param name="t">instant [s]</param>
    ''' <returns></returns>
    Private Function modC(t As Double) As Double
        If Me.cycl <= 0 Then
            Throw New Exception("Cannot apply the modulo operator without a valid cycle time value")
        End If

        Dim n As Integer
        Dim sign As Integer = If(t >= 0, 1, -1)

        While t * sign > (n + 0.5) * Me.cycl
            n += 1
        End While

        Return t - (Me.cycl * n) * sign

    End Function

    Private Function Sum(ByVal values As Double(), ByVal from_index As Integer, ByVal to_index As Integer) As Double

        Dim tot As Double = 0
        Dim n As Integer = values.Length

        If from_index >= 0 AndAlso from_index <= n - 1 _
            AndAlso to_index >= 0 AndAlso to_index <= n - 1 Then

            For i As Integer = from_index To to_index
                tot += values(i)
            Next

        Else
            Throw New ArgumentOutOfRangeException
        End If

        Return tot

    End Function



#End Region


    Public Sub Dump(filename As String)
        'Stop
        Dim out_folder As String = "..\..\..\..\..\bad_corridors"
        Directory.CreateDirectory(out_folder)
        Dim fw As New System.IO.StreamWriter(Path.Combine(out_folder, filename), True)

        Try
            fw.WriteLine("JUNC{0}{0}GINI{0}GEND{0}{0}GIN2{0}GEN2{0}{0}thet{0}{0}d{0}{0}d0", ControlChars.Tab)
            For j As Integer = 0 To Me.njunc - 1
                fw.WriteLine("{1}{0}{0}{0}{2}{0}{0}{3}{0}{0}{0}{4}{0}{0}{5}{0}{0}{0}{6:0.0}{0}{0}{7:0.0}{0}{8:0.0}", ControlChars.Tab,
                             j,
                             Me.gini(j),
                             Me.gend(j),
                             Me.gini2(j),
                             Me.gend2(j),
                             Me.AbsoluteOffset(False)(j),
                             Me.InternalOffset(j),
                             Me.InternalOffsetZero(j))

            Next
            fw.WriteLine()
            fw.WriteLine("------------------------------------------------------------------------")
            fw.WriteLine()

        Catch ex As Exception
            Stop
        End Try
        fw.Close()
    End Sub

End Class
