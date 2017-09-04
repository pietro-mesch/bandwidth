Imports Microsoft.SolverFoundation.Common
Imports Microsoft.SolverFoundation.Solvers

Public Class BandMaximiser
    ''' <summary>
    ''' Bandwidth
    ''' </summary>
    Private _B As Double

    ''' <summary>
    ''' Cycle time [s]
    ''' </summary>
    Private _C As Double

    ''' <summary>
    ''' g(j) is the green duration of the through phase at junction j, with j € [0, n-1]
    ''' </summary>
    Private _g As Double()

    ''' <summary>
    ''' _t_o(j) is the position of the midgreen wrt the nearest multiple of C at junction j, with j € [0, n-1]
    ''' </summary>
    Private _t_o As Double()

    ''' <summary>
    ''' t(j) is the travel time from junction j to junction j+1, with j € [0, n-1]
    ''' </summary>
    Private _t As Double()

    ''' <summary>
    ''' number of junctions in the path
    ''' </summary>
    Private n As Integer


    Public Sub New(ByVal cycl As Double, ByVal trav As Double(),
                                  ByVal gini As Double(), ByVal gend As Double())
        'PM CHECK INPUT
        If cycl <= 0 Then
            Throw New ArgumentOutOfRangeException
        End If

        If gini.Length <> gend.Length Then
            Throw New ArgumentException
        End If

        If trav.Length <> gini.Length - 1 Then
            Throw New ArgumentException
        End If

        n = gini.Length 'number of junctions

        'PREPROCESSING
        _C = cycl

        'GREEN DURATION
        _g = ComputeGreenDuration(gini, gend)

        'INTERNAL OFFSET
        _t_o = ComputeInternalOffsets(gini, gend)

        'TRAVEL TIMES
        _t = trav

    End Sub

    ''' <summary>
    ''' Calculate the offsets along the path that maximise the green bandwidth in the main direction only
    ''' </summary>
    ''' <returns></returns>
    Public Function OneWayOffsets() As Double()
        Dim offs(n - 1) As Double

        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        '' INPUTS TO ONE WAY OPTIMISATION                                                       ''
        '' the variables vector has dimensions n+1, for n relative offsets and the bandwidth    ''
        '' the linear program goes as follows:                                                  ''
        ''
        '' MAX b                                                                                ''
        '' SUBJECT TO b <= t_d(i) - t_d(j) + (g(i) + g(j))/2 for each i <> j € [0, n-1]         ''
        ''            b >= 0                                                                    ''
        ''            b <= g.min                                                                ''
        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

#If DEBUG Then
        Console.WriteLine("One Way Bandwidth Optimisation, fixed travel times, {0} junctions.", n)
        Dim time As New Stopwatch
        time.Start()
#End If

        Dim solver As New SimplexSolver()
        'add variables
        Dim b As Integer
        solver.AddVariable("b", b)
        solver.SetBounds(b, 0, _g.Min)

        'the internal offsets
        Dim w(n - 1) As Integer
        For j As Integer = 0 To n - 1
            solver.AddVariable("w" & j, w(j))
            solver.SetBounds(w(j), -_C / 2, _C / 2)
        Next

        'the n x n-1 constraints
        Dim constraint(n - 1)() As Integer
        For i As Integer = 0 To n - 1
            ReDim constraint(i)(n - 1)
            For j As Integer = 0 To n - 1
                If i <> j Then
                    solver.AddRow(String.Format("i{0}, j{1}", i, j), constraint(i)(j))
                    solver.SetCoefficient(constraint(i)(j), b, 1)
                    solver.SetCoefficient(constraint(i)(j), w(i), -1)
                    solver.SetCoefficient(constraint(i)(j), w(j), 1)
                    solver.SetBounds(constraint(i)(j), Rational.NegativeInfinity, _g(i) / 2 + _g(j) / 2)
                End If
            Next
        Next

        'the bandwidth (objective function)
        Dim bandwidth As Integer
        solver.AddRow("bandwidth", bandwidth)
        solver.SetCoefficient(bandwidth, b, 1)
        solver.AddGoal(bandwidth, 1, False)

        'solve the linear problem
        solver.Solve(New SimplexSolverParams())

        'extract values
        _B = solver.GetValue(bandwidth).ToDouble

        Dim w0 As Double = solver.GetValue(w(0)).ToDouble
        Dim to0 As Double = _t_o(0)
        For j As Integer = 0 To n - 1
            offs(j) = modC((to0 - w0 + solver.GetValue(w(j)).ToDouble + If(j > 0, SumT(0, j - 1), 0))) - _t_o(j)
            If offs(j) < 0 Then offs(j) += _C
        Next

#If DEBUG Then
        time.Stop()
        Console.WriteLine("Green duration : min = {0} s , MAX = {1} s", _g.Min, _g.Max)
        Console.WriteLine("Solution time : {0} ms", time.ElapsedMilliseconds)
        Console.WriteLine("Bandwidth : {0} s [{1:0.0%}]", _B, _B / _g.Min)
        Console.WriteLine("OFFSET VALUES:")
        For j As Integer = 0 To n - 1
            Console.Write("{0} ", offs(j))
        Next
        Console.WriteLine("")
        Console.WriteLine("")
#End If

        time = Nothing

        Return offs
    End Function

#Region "PRIVATE METHODS"

    ''' <summary>
    ''' Returns the distance of t to the nearest integer cycle
    ''' </summary>
    ''' <param name="t">instant [s]</param>
    ''' <returns></returns>
    Private Function modC(t As Double) As Double
        If _C <= 0 Then
            Throw New Exception("Cannot apply the modulo operator without a valid cycle time value")
        End If

        Dim n As Integer
        While t > (n + 0.5) * _C
            n += 1
        End While

        Return t - (_C * n)

    End Function

    Private Function SumT(ByVal from_index As Integer, ByVal to_index As Integer) As Double
        If _t Is Nothing Then
            Throw New Exception("Travel times not defined")
        End If
        Dim sum As Integer = 0

        If from_index >= 0 AndAlso from_index <= n - 1 _
            AndAlso to_index >= 0 AndAlso to_index <= n - 1 Then

            For i As Integer = from_index To to_index
                sum += _t(i)
            Next

        Else
            Throw New ArgumentOutOfRangeException
        End If

        Return sum

    End Function

    Private Function ComputeGreenDuration(ByVal gini As Double(), ByVal gend As Double()) As Double()
        Dim duration(n - 1) As Double
        For j As Integer = 0 To n - 1
            duration(j) = gend(j) - gini(j)
            If duration(j) < 0 Then duration(j) += _C
            If duration(j) <= 0 Then
                Throw New ArgumentException
            End If
        Next

        Return duration
    End Function

    Private Function ComputeInternalOffsets(ByVal gini As Double(), ByVal gend As Double()) As Double()
        Dim midpoint As Double
        Dim offset(n - 1) As Double
        For j As Integer = 0 To n - 1

            'first find green midpoint
            If gini(j) < gend(j) Then
                midpoint = (gini(j) + gend(j)) / 2
            Else
                midpoint = (gini(j) + gend(j) + _C) / 2
                If midpoint >= _C Then
                    midpoint -= _C
                End If
            End If

            'find its distance to the nearest cycle
            offset(j) = modC(midpoint)
        Next

        Return offset

    End Function

#End Region

End Class
