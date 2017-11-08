Option Strict On
Imports Gurobi
Imports Microsoft.SolverFoundation.Common
Imports Microsoft.SolverFoundation.Solvers

Public Class BandMaximiser

    Private _theCorridor As t_CORRIDOR

    ''' <summary>
    ''' Bandwidth
    ''' </summary>
    Private _B As Double = 0

    ''' <summary>
    ''' Secondary Bandwidth
    ''' </summary>
    Private _B2 As Double = 0

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

    Public Sub New(c As t_CORRIDOR)

        _theCorridor = c

        n = c.njunc 'number of junctions

        'PREPROCESSING
        _C = c.cycl

        'TRAVEL TIMES
        _t = c.trav
        If c.trav2 Is Nothing Then
            _t2 = c.trav.Select(Function(x) -x).ToArray
        Else
            _t2 = c.trav2
        End If

        'IF ONLY ONE SET OF GINIGEND WAS PROVIDED, ASSUME THAT THE THROUGH PHASE SERVES BOTH DIRECTIONS
        'GREEN DURATION
        _g = c.GreenDuration(False)
        _g2 = c.GreenDuration(True)

        'ABSOLUTE OFFSET
        _t_o = c.AbsoluteOffset(False)

        'INTERNAL OFFSET
        _t_d0 = c.InternalOffsetZero()

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

    Public Function GetOffsetsOneWayBand() As Double()
        Dim offs(n - 1) As Double

#If DEBUG Then
        Console.WriteLine("One Way Bandwidth Optimisation, fixed travel times, {0} junctions.", n)
        'Dim time As New Stopwatch
        'time.Start()
#End If

        Dim env As New GRBEnv("LP.log")
        Dim model As New GRBModel(env)

        'add variables
        Dim b As GRBVar = model.AddVar(0, _g.Min, 0, GRB.CONTINUOUS, "main band")

        'the relative offsets
        Dim w(n - 1) As GRBVar
        For j As Integer = 0 To n - 1
            w(j) = model.AddVar(-_C / 2, _C / 2, 0, GRB.CONTINUOUS, "w" & j)
        Next

        'add objective
        model.SetObjective(1 * b, GRB.MAXIMIZE)

        'the n x n-1 constraints for the main direction
        For i As Integer = 0 To n - 1
            For j As Integer = 0 To n - 1
                If i <> j Then
                    model.AddConstr(b - w(i) + w(j) <= _g(i) / 2 + _g(j) / 2, String.Format("i{0}, j{1}", i, j))
                End If
            Next
        Next

        Me.ResetBandwidth()
        'solve the linear problem
        model.Optimize()

#If DEBUG Then
        If model.Status <> 2 Then
            _theCorridor.Dump("infeasible.txt")
            Stop
        End If
#End If

        'extract values
        _B = b.X

        Dim w0 As Double = w(0).X
        Dim to0 As Double = _t_o(0)
        For j As Integer = 0 To n - 1
            offs(j) = modC((to0 - w0 + w(j).X + If(j > 0, Sum(_t, 0, j - 1), 0))) - _t_o(j)
            If offs(j) < 0 Then offs(j) += _C
        Next

        'compute actual bandwidth
        Dim B_real As Double = Double.PositiveInfinity
        For i As Integer = 0 To n - 1
            For j As Integer = 0 To n - 1
                If i <> j Then
                    B_real = Math.Min(B_real, w(i).X - w(j).X + _g(i) / 2 + _g(j) / 2)
                End If
            Next j
        Next i
        B_real = Math.Max(0, B_real)



#If DEBUG Then
        'time.Stop()
        Console.WriteLine("Green duration : min = {0} s , MAX = {1} s", _g.Min, _g.Max)
        Console.WriteLine("     secondary : min = {0} s , MAX = {1} s", _g2.Min, _g2.Max)

        'Console.WriteLine("Solution time : {0} ms", time.ElapsedMilliseconds)
        Console.WriteLine("Bandwidth 1 : {0:0} s [{1:0.0%}] --- {2:0} s [{3:0.0%}]", _B, _B / _g.Min, B_real, B_real / _g.Min)
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

    Public Function GetOffsetsOneWaySlackBand() As Double()
        Dim offs(n - 1) As Double

#If DEBUG Then
        Console.WriteLine("One Way Slack Bandwidth Optimisation, fixed travel times, {0} junctions.", n)
        'Dim time As New Stopwatch
        'time.Start()
#End If

        'seems you CAN'T do this with a linear program


        Dim B_real As Double = Double.PositiveInfinity
        Dim B2_real As Double = Double.PositiveInfinity

#If DEBUG Then
        'time.Stop()
        Console.WriteLine("Green duration : min = {0} s , MAX = {1} s", _g.Min, _g.Max)
        Console.WriteLine("     secondary : min = {0} s , MAX = {1} s", _g2.Min, _g2.Max)

        'Console.WriteLine("Solution time : {0} ms", time.ElapsedMilliseconds)
        Console.WriteLine("Bandwidth 1 : {0:0} s [{1:0.0%}] --- {2:0} s [{3:0.0%}]", _B, _B / _g.Min, B_real, B_real / _g.Min)
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


    ''' <summary>
    ''' Calculate the offsets along the path that maximise the green bandwidth in the main direction only
    ''' </summary>
    ''' <param name="secondary_direction_weight"> the weight of the secondary bandwidth in the optimisation (0,1) </param>
    ''' <returns></returns>
    Public Function GetOffsetsTwoWayBand(Optional ByVal secondary_direction_weight As Double = 0.1) As Double()
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

        Dim env As New GRBEnv("LP.log")
        Dim model As New GRBModel(env)

        'add variables
        Dim b As GRBVar = model.AddVar(Double.NegativeInfinity, _g.Min, 0, GRB.CONTINUOUS, "main band")
        Dim b2 As GRBVar = model.AddVar(Double.NegativeInfinity, _g2.Min, 0, GRB.CONTINUOUS, "return band")

        'the relative offsets
        Dim w(n - 1) As GRBVar
        For j As Integer = 0 To n - 1
            w(j) = model.AddVar(-_C / 2, _C / 2, 0, GRB.CONTINUOUS, "w" & j)
        Next

        'add objective
        model.SetObjective((1 - secondary_direction_weight) * b + secondary_direction_weight * b2, GRB.MAXIMIZE)

        'the n x n-1 constraints for the main direction
        For i As Integer = 0 To n - 1
            For j As Integer = 0 To n - 1
                If i <> j Then
                    model.AddConstr(b - w(i) + w(j) <= _g(i) / 2 + _g(j) / 2, String.Format("i{0}, j{1}", i, j))
                End If
            Next
        Next

        'the n x n-1 constraints for the return direction
        For i As Integer = 0 To n - 1
            For j As Integer = 0 To n - 1
                If i <> j Then
                    model.AddConstr(b2 - w(i) + w(j) <= _g2(i) / 2 + _g2(j) / 2 + _t_d0(i) - _t_d0(j), String.Format("ret i{0}, j{1}", i, j))
                    'model.AddConstr(b2 - w(i) + w(j) <= _g2(i) / 2 + _g2(j) / 2 + modC(_t_d0(i) - _t_d0(j)), String.Format("ret i{0}, j{1}", i, j))
                End If
            Next
        Next

        Me.ResetBandwidth()
        'solve the linear problem
        model.Optimize()

#If DEBUG Then
        'CHECK MODEL FEASIBILITY
        Select Case model.Status
            Case 2
                'ALL GOOD

            Case 3
                Stop
                _theCorridor.Dump("infeasible.txt")
                Return {-1}

            Case 5
                Stop
                _theCorridor.Dump("unbounded.txt")

            Case Else
                Stop

        End Select
#End If

        'extract values
        _B = b.X
        _B2 = b2.X

        Dim w0 As Double = w(0).X
        Dim to0 As Double = _t_o(0)
        For j As Integer = 0 To n - 1
            offs(j) = modC((to0 - w0 + w(j).X + If(j > 0, Sum(_t, 0, j - 1), 0))) - _t_o(j)
            If offs(j) < 0 Then offs(j) += _C
        Next

        'compute actual bandwidth
        Dim B_real As Double = Double.PositiveInfinity
        Dim B2_real As Double = Double.PositiveInfinity
        For i As Integer = 0 To n - 1
            For j As Integer = 0 To n - 1
                If i <> j Then
                    B_real = Math.Min(B_real, w(i).X - w(j).X + _g(i) / 2 + _g(j) / 2)
                    B2_real = Math.Min(B2_real, w(i).X - w(j).X + _g2(i) / 2 + _g2(j) / 2 + _t_d0(i) - _t_d0(j))
                End If
            Next j
        Next i
        B_real = Math.Max(0, B_real)
        B2_real = Math.Max(0, B2_real)


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

    Private Sub ResetBandwidth()
        Me._B = 0
        Me._B2 = 0
    End Sub

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
        Dim sign As Integer = If(t >= 0, 1, -1)

        While t * sign > (n + 0.5) * _C
            n += 1
        End While

        Return t - (_C * n) * sign

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


#End Region

End Class
