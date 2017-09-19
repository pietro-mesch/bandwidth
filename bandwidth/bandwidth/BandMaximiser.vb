Option Strict On

Imports Microsoft.SolverFoundation.Common
Imports Microsoft.SolverFoundation.Solvers

Public Class BandMaximiser
    ''' <summary>
    ''' Bandwidth
    ''' </summary>
    Private _B As Double

    ''' <summary>
    ''' Secondary Bandwidth
    ''' </summary>
    Private _B2 As Double

    ''' <summary>
    ''' Cycle time [s]
    ''' </summary>
    Private _C As Double

    ''' <summary>
    ''' g(j) is the green duration of the main through phase at junction j, with j € [0, n-1]
    ''' </summary>
    Private _g As Double()
    ''' <summary>
    ''' g2(j) is the green duration of the secondary through phase at junction j, with j € [0, n-1]
    ''' </summary>
    Private _g2 As Double()


    ''' <summary>
    ''' _t_o(j) is the position of the through phase in the main direction wrt the nearest multiple of C at junction j, with j € [0, n-1]
    ''' </summary>
    Private _t_o As Double()

    ''' <summary>
    ''' _t_o(j) is the position of the through phase in the secondary direction wrt that in the main direction at junction j, with j € [0, n-1]
    ''' </summary>
    Private _t_d0 As Double()

    ''' <summary>
    ''' t(j) is the travel time from junction j to junction j+1, with j € [0, n-1]
    ''' </summary>
    Private _t As Double()

    ''' <summary>
    ''' t2(j) is the travel time from junction j+1 to junction j, with j € [0, n-1]
    ''' </summary>
    Private _t2 As Double()

    ''' <summary>
    ''' number of junctions in the path
    ''' </summary>
    Private n As Integer


    Public Sub New(ByVal cycl As Double, ByVal trav As Double(),
                                  ByVal gini As Integer(), ByVal gend As Integer(),
                   Optional ByVal trav2 As Double() = Nothing,
                   Optional ByVal gini2 As Integer() = Nothing,
                   Optional ByVal gend2 As Integer() = Nothing)
        'PM CHECK INPUT
        If cycl <= 0 Then
            Throw New ArgumentOutOfRangeException
        End If

        If gini.Length <> gend.Length Then
            Throw New ArgumentException
        End If
        If gini2 IsNot Nothing OrElse gend2 IsNot Nothing Then
            If gini2.Length <> gend.Length OrElse gend2.Length <> gend.Length Then
                Throw New ArgumentException
            End If
        End If

        If trav.Length <> gini.Length - 1 Then
            Throw New ArgumentException
        End If
        If trav2 IsNot Nothing AndAlso trav2.Length <> gini.Length - 1 Then
            Throw New ArgumentException
        End If

        n = gini.Length 'number of junctions

        'PREPROCESSING
        _C = cycl

        'TRAVEL TIMES
        _t = trav
        If trav2 Is Nothing Then trav2 = trav.Select(Function(x) -x).ToArray
        _t2 = trav2

        'IF ONLY ONE SET OF GINIGEND WAS PROVIDED, ASSUME THAT THE THROUGH PHASE SERVES BOTH DIRECTIONS
        'GREEN DURATION
        _g = ComputeGreenDuration(gini, gend)
        If gini2 Is Nothing AndAlso gend2 Is Nothing Then
            gini2 = gini
            gend2 = gend
        End If
        _g2 = ComputeGreenDuration(gini2, gend2)

        'ABSOLUTE OFFSET
        _t_o = ComputeAbsoluteOffsets(gini, gend)

        'INTERNAL OFFSET
        _t_d0 = ComputeInternalOffsets(gini, gend, gini2, gend2)


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
        'Dim time As New Stopwatch
        'time.Start()
#End If

        Dim solver As New SimplexSolver()
        'add variables
        Dim b As Integer
        solver.AddVariable("b", b)
        solver.SetBounds(b, 0, _g.Min)

        'the relative offsets
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
            offs(j) = modC((to0 - w0 + solver.GetValue(w(j)).ToDouble + If(j > 0, Sum(_t, 0, j - 1), 0))) - _t_o(j)
            If offs(j) < 0 Then offs(j) += _C
        Next

#If DEBUG Then
        'time.Stop()
        Console.WriteLine("Green duration : min = {0} s , MAX = {1} s", _g.Min, _g.Max)
        'Console.WriteLine("Solution time : {0} ms", time.ElapsedMilliseconds)
        Console.WriteLine("Bandwidth : {0} s [{1:0.0%}]", _B, _B / _g.Min)
        Console.WriteLine("OFFSET VALUES:")
        For j As Integer = 0 To n - 1
            Console.Write("{0} ", offs(j))
        Next
        Console.WriteLine("")
        Console.WriteLine("")
#End If

        'time = Nothing

        Return offs
    End Function


    ''' <summary>
    ''' Calculate the offsets along the path that maximise the green bandwidth in the main direction only
    ''' </summary>
    ''' <param name="secondary_direction_weight"> the weight of the secondary bandwidth in the optimisation (0,1) </param>
    ''' <returns></returns>
    Public Function TwoWayOffsets(Optional ByVal secondary_direction_weight As Double = 0.1) As Double()
        Dim offs(n - 1) As Double

        If secondary_direction_weight > 1 Then Throw New ArgumentOutOfRangeException("The secondary bandwidth weight cannot be more than 1")
        If secondary_direction_weight < 0 Then Throw New ArgumentOutOfRangeException("The secondary bandwidth weight cannot be negative")

        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        '' INPUTS TO TWO WAY OPTIMISATION                                                                      ''
        '' the variables vector has dimensions n+2, for n relative offsets and the bandwidth in each direction ''
        '' the linear program goes as follows:                                                                 ''
        ''                                                                                                     ''
        '' MAX b + b2                                                                                          ''
        '' SUBJECT TO b <= t_d(i) - t_d(j) + (g(i) + g(j))/2 for each i <> j € [0, n-1]                        ''
        ''            b >= 0                                                                                   ''
        ''            b <= g.min                                                                               ''
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

#If DEBUG Then
        Console.WriteLine("Two Way Bandwidth Optimisation, fixed travel times, {0} junctions.", n)
        'Dim time As New Stopwatch
        'time.Start()
#End If

        Dim solver As New SimplexSolver()
        'add variables
        Dim b, b2 As Integer
        solver.AddVariable("b", b)
        solver.AddVariable("b2", b2)
        'PM bandwidth variable bounds
        'solver.SetBounds(b, 0, _g.Min)
        'solver.SetBounds(b2, 0, _g2.Min)

        'the relative offsets
        Dim w(n - 1) As Integer
        For j As Integer = 0 To n - 1
            solver.AddVariable("w" & j, w(j))
            solver.SetBounds(w(j), -_C / 2, _C / 2)
        Next

        'the n x n-1 constraints for the main direction
        Dim constraint(n - 1)() As Integer
        For i As Integer = 0 To n - 1
            ReDim constraint(i)(n - 1)
            For j As Integer = 0 To n - 1
                If i <> j Then
                    solver.AddRow(String.Format("i{0}, j{1}", i, j), constraint(i)(j))
                    solver.SetCoefficient(constraint(i)(j), b, 1)
                    solver.SetCoefficient(constraint(i)(j), w(i), -1)
                    solver.SetCoefficient(constraint(i)(j), w(j), 1)
                    'solver.SetBounds(constraint(i)(j), Rational.NegativeInfinity, _g(i) / 2 + _g(j) / 2)
                    solver.SetUpperBound(constraint(i)(j), _g(i) / 2 + _g(j) / 2)
                End If
            Next
        Next

        'the n x n-1 constraints for the secondary direction
        Dim constraint2(n - 1)() As Integer
        For i As Integer = 0 To n - 1
            ReDim constraint2(i)(n - 1)
            For j As Integer = 0 To n - 1
                If i <> j Then
                    solver.AddRow(String.Format("i{0}, j{1} secondary", i, j), constraint2(i)(j))
                    solver.SetCoefficient(constraint2(i)(j), b2, 1)
                    solver.SetCoefficient(constraint2(i)(j), w(i), -1)
                    solver.SetCoefficient(constraint2(i)(j), w(j), 1)
                    'solver.SetBounds(constraint2(i)(j), Rational.NegativeInfinity, _g2(i) / 2 + _g2(j) / 2 + _t_d0(i) - _t_d0(j))
                    solver.SetUpperBound(constraint2(i)(j), _g2(i) / 2 + _g2(j) / 2 + _t_d0(i) - _t_d0(j))
                End If
            Next
        Next

        'PM bandwidth constraints
        Dim blim, b2lim As Integer
        solver.AddRow("b constraint", blim)
        solver.SetCoefficient(blim, b, 1)
        solver.SetBounds(blim, 0, _g.Min)
        solver.AddRow("b2 constraint", b2lim)
        solver.SetCoefficient(b2lim, b2, 1)
        solver.SetBounds(b2lim, 0, _g2.Min)

        'the bandwidth (objective function)
        Dim bandwidth As Integer
        solver.AddRow("two way bandwidth", bandwidth)
        solver.SetCoefficient(bandwidth, b, 1 - secondary_direction_weight)
        If secondary_direction_weight > 0 Then
            solver.SetCoefficient(bandwidth, b2, secondary_direction_weight)
        End If
        solver.AddGoal(bandwidth, 1, False)

        'solve the linear problem
        solver.Solve(New SimplexSolverParams())

        'extract values
        _B = solver.GetValue(b).ToDouble
        _B2 = solver.GetValue(b2).ToDouble

        Dim w0 As Double = solver.GetValue(w(0)).ToDouble
        Dim to0 As Double = _t_o(0)
        For j As Integer = 0 To n - 1
            offs(j) = modC((to0 - w0 + solver.GetValue(w(j)).ToDouble + If(j > 0, Sum(_t, 0, j - 1), 0))) - _t_o(j)
            If offs(j) < 0 Then offs(j) += _C
        Next

        'compute actual bandwidth
        Dim B_real As Double = Double.PositiveInfinity
        Dim B2_real As Double = Double.PositiveInfinity
        For i As Integer = 0 To n - 1
            For j As Integer = 0 To n - 1
                If i <> j Then
                    B_real = Math.Min(B_real, solver.GetValue(w(i)).ToDouble - solver.GetValue(w(j)).ToDouble + _g(i) / 2 + _g(j) / 2)
                    B2_real = Math.Min(B2_real, solver.GetValue(w(i)).ToDouble - solver.GetValue(w(j)).ToDouble + _g(i) / 2 + _g(j) / 2 + _t_d0(i) - _t_d0(j))
                End If

            Next
        Next


#If DEBUG Then
        'time.Stop()
        Console.WriteLine("Green duration : min = {0} s , MAX = {1} s", _g.Min, _g.Max)
        Console.WriteLine("     secondary : min = {0} s , MAX = {1} s", _g2.Min, _g2.Max)

        'Console.WriteLine("Solution time : {0} ms", time.ElapsedMilliseconds)
        Console.WriteLine("Bandwidth 1 : {0:0} s [{1:0.0%}] --- {2:0} s [{3:0.0%}]", _B, _B / _g.Min, B_real, B_real / _g.Min)
        Console.WriteLine("Bandwidth 2 : {0:0} s [{1:0.0%}] --- {2:0} s [{3:0.0%}]", _B2, _B2 / _g2.Min, B2_real, B2_real / _g2.Min)
        Console.WriteLine("OFFSET VALUES:")
        For j As Integer = 0 To n - 1
            Console.Write("{0:0} ", offs(j))
        Next
        Console.WriteLine("")
        Console.WriteLine("")
#End If

        'time = Nothing

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

    Private Function Sum(ByVal values As Double(), ByVal from_index As Integer, ByVal to_index As Integer) As Double

        Dim tot As Double = 0

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

    Private Function ComputeGreenDuration(ByVal gini As Integer(), ByVal gend As Integer()) As Double()
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

    Private Function ComputeAbsoluteOffsets(ByVal gini As Integer(), ByVal gend As Integer()) As Double()
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

    ''' <summary>
    ''' Computes the internal offsets mapped to the FoR of the first junction
    ''' </summary>
    ''' <param name="gini"></param>
    ''' <param name="gend"></param>
    ''' <param name="gini2"></param>
    ''' <param name="gend2"></param>
    ''' <returns></returns>
    Private Function ComputeInternalOffsets(ByVal gini As Integer(), ByVal gend As Integer(), ByVal gini2 As Integer(), ByVal gend2 As Integer()) As Double()

        Dim offset As Double() = ComputeAbsoluteOffsets(gini, gend)
        Dim offset2 As Double() = ComputeAbsoluteOffsets(gini2, gend2)

        Dim internal(n - 1) As Double

        For i As Integer = 0 To n - 1
            internal(i) = modC(modC(offset2(i) - offset(i)) + If(i > 0, Sum(_t, 0, i - 1) - Sum(_t2, 0, i - 1), 0))
        Next

        Return internal

    End Function
#End Region

End Class
