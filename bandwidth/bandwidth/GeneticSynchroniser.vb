Public Class GeneticSynchroniser

#Region "DECLARATIONS"
#Region "Optimisation Status"
    Private _param As GSparameters
    Private _status As GSstatus

    Private _theCorridor As t_CORRIDOR

    Private _s As Double()()
    Private _fitness As Double()

    ''' <summary>
    ''' Chromosome size (number of genes per individual)
    ''' </summary>
    Private _n As Integer

    ''' <summary>
    ''' Population size (number of individuals)
    ''' </summary>
    Private _popSize As Integer

    ''' <summary>
    ''' The index of the first UNFIT individual marked for death
    ''' </summary>
    Private _bottleneckIndex As Integer

#End Region

#Region "Corridor Data"
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
    ''' Cycle time [s]
    ''' </summary>
    Private _C As Double

    ''' <summary>
    ''' t(j) is the travel time from junction j to junction j+1, with j € [0, n-1]
    ''' </summary>
    Private _t As Double()

    ''' <summary>
    ''' t2(j) is the travel time from junction j+1 to junction j, with j € [0, n-1]
    ''' </summary>
    Private _t2 As Double()

#End Region

#End Region

    Public Sub New(corridor As t_CORRIDOR)
        _param = New GSparameters
        _status = New GSstatus
        InitOptimisation()

        _theCorridor = corridor
        GetCorridorData()

        Dim i, s As Integer
        _popSize = _param.populationSize

        'initialise solution array
        ReDim _s(_popSize - 1)
        ReDim _fitness(_popSize - 1)
        Dim r As New Random(Now.Millisecond)

        For s = 0 To _popSize - 1
            ReDim _s(s)(_n - 1)
            For i = 0 To _n - 1
                _s(s)(i) = r.Next(-_theCorridor.cycl * 5, _theCorridor.cycl * 5 + 1) / 10
            Next
        Next


    End Sub

    Private Sub InitOptimisation()
        If _param.bandwidthType = BandwidthType.TwoWaySlack Then
            _status.maximising = True
        End If

        _status.initHistory(_param.maxGenWithoutImprovement)

    End Sub

    Private Sub GetCorridorData()
        'precalculate corridor data
        _n = _theCorridor.njunc

        'CYCLE TIME
        _C = _theCorridor.cycl

        'TRAVEL TIMES
        _t = _theCorridor.trav
        _t2 = _theCorridor.trav2
        If _t2 Is Nothing Then
            _t2 = _theCorridor.trav.Select(Function(x) -x).ToArray
        End If

        'GREEN DURATION
        _g = _theCorridor.GreenDuration(False)
        _g2 = _theCorridor.GreenDuration(True)

        'ABSOLUTE OFFSET
        _t_o = _theCorridor.AbsoluteOffset(False)

        'INTERNAL OFFSET
        _t_d0 = _theCorridor.InternalOffsetZero()
    End Sub

    Public Sub Optimize()
        'PM REM '''''''''''''''''
        'Stop
        Dim time As New Stopwatch
        time.Start()
        '''''''''''''''''''''''''
        CalculatePopulationFitness()

        While Not TerminationConditionsMet()

            AutoAdjust()

            BreedTheFit()

            UpdatePopulationRanking()

            _status.generation += 1

        End While

        _theCorridor.offs = AbsoluteOffsets(_s(0))

        'PM REM '''''''''''''''''
        time.Stop()
        Console.WriteLine("Generation {0} : {1} ms - fitness {2}", _status.generation, time.ElapsedMilliseconds, _status.bestFitness)
        'Stop
        '''''''''''''''''''''''''

    End Sub

    Private Function TerminationConditionsMet() As Boolean

        Dim terminate As Boolean = False

        'terminate for threshold reached
        terminate = _status.bestFitness <= _param.fitnessThreshold AndAlso Not _status.maximising

        'terminate for no improvements 
        terminate = terminate OrElse _status.NoImprovements

        'terminate for generation limit reached
        terminate = terminate OrElse _status.generation > _param.maxGenerations

        Return terminate

    End Function

    Private Function AbsoluteOffsets(w() As Double) As Integer()

        Dim offset(_n - 1) As Double

        Dim w0 As Double = w(0)
        Dim to0 As Double = _t_o(0)
        For j As Integer = 0 To _n - 1
            offset(j) = modC((to0 - w0 + w(j) + If(j > 0, Sum(_t, 0, j - 1), 0))) - _t_o(j)
            If offset(j) < 0 Then offset(j) += _C
        Next

        'round up and return integer values
        Return offset.Select(Function(x) CInt(Math.Round(x))).ToArray

    End Function

    Private Sub CalculatePopulationFitness()

        For s As Integer = 0 To _popSize - 1
            _fitness(s) = CalculateIndividualFitness(_s(s))
        Next s

        UpdatePopulationRanking()

    End Sub

    Private Sub UpdatePopulationRanking()
        SortByFitness(_s, _fitness)
        If _status.maximising Then
            _s = _s.Reverse.ToArray
            _fitness = _fitness.Reverse.ToArray
        End If
        _status.bestFitness = _fitness(0)
        _status.averageFitness = _fitness.Average
    End Sub

    Private Function CalculateIndividualFitness(w() As Double) As Double

        Dim fitness As Double

        Select Case _param.bandwidthType
            Case BandwidthType.OneWayHard

                fitness = CalculateIndividualFitness_OneWayHard(w)


            Case BandwidthType.TwoWayHard

                fitness = CalculateIndividualFitness_TwoWayHard(w)


            Case BandwidthType.TwoWaySlack

                fitness = CalculateIndividualFitness_TwoWaySlack(w)

            Case Else
                Throw New NotImplementedException

        End Select

        Return fitness
    End Function


    Private Function CalculateIndividualFitness_OneWayHard(w() As Double) As Double

        'compute actual bandwidth
        Dim fitness As Double = Double.PositiveInfinity
        For i As Integer = 0 To _n - 1
            For j As Integer = 0 To _n - 1
                If i <> j Then
                    fitness = Math.Min(fitness, w(i) - w(j) + _g(i) / 2 + _g(j) / 2)
                End If
            Next j
        Next i
        fitness = Math.Max(0, fitness)
        fitness = Math.Min(_g.Min, fitness)

        'the fitness index is the percent distance from the perfect bandwidth (just keeping it simple)
        Return 1 - fitness / _g.Min

    End Function

    Private Function CalculateIndividualFitness_TwoWayHard(w() As Double) As Double

        'compute bandwidth in both directions
        Dim f1 As Double = Double.PositiveInfinity
        Dim f2 As Double = Double.PositiveInfinity
        For i As Integer = 0 To _n - 1
            For j As Integer = 0 To _n - 1
                If i <> j Then
                    f1 = Math.Min(f1, w(i) - w(j) + _g(i) / 2 + _g(j) / 2)
                    f2 = Math.Min(f2, w(i) - w(j) + _g2(i) / 2 + _g2(j) / 2 + _t_d0(i) - _t_d0(j))
                End If
            Next j
        Next i
        f1 = Math.Max(0, f1)
        f2 = Math.Max(0, f2)
        f1 = Math.Min(_g.Min, f1)
        f2 = Math.Min(_g2.Min, f2)

        'the fitness index is the percent distance from the perfect bandwidth (just keeping it simple)
        f1 = 1 - f1 / _g.Min
        f2 = 1 - f2 / _g2.Min

        Return f1 * (1 - _param.secondaryBandWeight) + f2 * _param.secondaryBandWeight

    End Function

    Private Function CalculateIndividualFitness_TwoWaySlack(w() As Double) As Double

        'compute bandwidth in both directions
        Dim f1(_n - 1) As Double
        Dim f2(_n - 1) As Double
        Dim B As Double = 0

        'main band
        For i As Integer = 0 To _n - 1
            For j As Integer = 0 To _n - 1
                If i <> j Then
                    'calc intersection between the two intervals
                    B = Math.Min(w(i) + _g(i) / 2, w(j) + _g(j) / 2) - Math.Max(w(i) - _g(i) / 2, w(j) - _g(j) / 2)

                    'if no intersection is found
                    If B <= 0 Then
                        If j < i Then
                            'if the band is interrupted on the way to the i-th junction no problem, just reset and add zero
                            f1(i) = 0
                            B = 0
                        Else
                            'if the band is interrupted downstream of the i-th junction, stop counting
                            Exit For
                        End If
                    End If

                    'accumulate band
                    f1(i) += B
                End If
            Next j
        Next i

        'secondary band
        For i As Integer = _n - 1 To 0 Step -1
            For j As Integer = _n - 1 To 0 Step -1
                If i <> j Then
                    'calc intersection between the two intervals
                    B = Math.Min(w(i) + _t_d0(i) + _g2(i) / 2, w(j) + _t_d0(j) + _g2(j) / 2) - Math.Max(w(i) + _t_d0(i) - _g2(i) / 2, w(j) + _t_d0(j) - _g2(j) / 2)

                    'if no intersection is found
                    If B <= 0 Then
                        If j > i Then
                            'if the band is interrupted on the way to the i-th junction no problem, just reset and add zero
                            f2(i) = 0
                            B = 0
                        Else
                            'if the band is interrupted downstream of the i-th junction, stop counting
                            Exit For
                        End If
                    End If

                    'accumulate band
                    f2(i) += B
                End If
            Next j
        Next i

        'the fitness is the cumulative green band (to be maximised)

        Return f1.Sum * (1 - _param.secondaryBandWeight) + f2.Sum * _param.secondaryBandWeight

    End Function

    ''' <summary>
    ''' Update Optimisation Parameters based on the current status and trend
    ''' </summary>
    Private Sub AutoAdjust()

        _bottleneckIndex = UpdateBottleneckIndex()

        _status.mutationGain = UpdateMutationGain()

    End Sub

    ''' <summary>
    ''' Increase mutation rate when the top individuals become too uniform
    ''' </summary>
    ''' <returns></returns>
    Private Function UpdateMutationGain() As Double

        Dim n As Integer
        While _fitness(n) = _fitness(0) AndAlso n < _bottleneckIndex
            n += 1
        End While

        'If n > 1 Then Stop

        Return 1 + (_param.maxMutationGain - 1) * (n - 1) / (_bottleneckIndex - 1)

    End Function

    Private Function UpdateBottleneckIndex() As Integer
        Select Case _param.thresholdUpdateMethod
            Case 0
                Return CInt(Math.Floor(_popSize * _param.thresholdPercentile)) + 1

            Case Else
                Throw New NotImplementedException

        End Select
    End Function

    Private Sub SortByFitness(ByRef solutions As Double()(), ByVal fitness() As Double)
        Array.Sort(fitness, solutions)
    End Sub



    Private Sub BreedTheFit()

        Dim r As New Random(Now.Millisecond)
        Dim A, B As Integer

        If _bottleneckIndex = 0 Then
            'busted
            Stop
        End If

        For s As Integer = _bottleneckIndex To _popSize - 1

            A = r.Next(0, _bottleneckIndex)
            B = r.Next(0, _bottleneckIndex)

            _s(s) = Breed(_s(A), _s(B))

            _fitness(s) = CalculateIndividualFitness(_s(s))

        Next s

    End Sub

    Private Function Breed(a As Double(), b As Double()) As Double()
        Dim r As New Random(Now.Millisecond)

        Dim x As Integer

        'generate random crossover points
        Dim crossindex(_param.crossoverPoints + 1) As Integer
        crossindex(_param.crossoverPoints + 1) = _n - 1
        For x = 1 To _param.crossoverPoints
            crossindex(x) = r.Next(0, _n)
        Next
        Array.Sort(crossindex)

        'copy parents into child
        Dim child(_n - 1) As Double
        Dim parent As Double()() = {a, b}
        Dim p As Integer = 0
        For x = 0 To _param.crossoverPoints
            Array.Copy(parent(p), crossindex(x), child, crossindex(x), crossindex(x + 1) - crossindex(x) + 1)
            p = Math.Abs(p - 1)
        Next

        ApplyMutations(child)

        Return child

    End Function

    Private Sub ApplyMutations(ByRef child() As Double)
        Dim r As New Random(Now.Millisecond)
        For i As Integer = 0 To _n - 1
            If r.Next(1, 101) > 100 * (1 - _param.mutationRate * _status.mutationGain) Then
                child(i) = MutateChromosome(child(i))
            End If
        Next
    End Sub

    Private Function MutateChromosome(original As Double) As Double
        'PM THIS MUTATION SCHEME IS STUPID
        Dim r As New Random(Now.Millisecond)

        'apply random mutation between -5 and +5
        Dim mutated As Double = original + r.Next(-50, 51) / 10
        If mutated > _C / 2 Then mutated -= _C
        If mutated < -_C / 2 Then mutated += _C

        Return mutated

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
        Dim sign As Integer = If(t >= 0, 1, -1)

        While t * sign > (n + 0.5) * _C
            n += 1
        End While

        Return t - (_C * n) * sign

    End Function

    Private Function Sum(ByVal values As Double(), ByVal from_index As Integer, ByVal to_index As Integer) As Double

        Dim tot As Double = 0

        If from_index >= 0 AndAlso from_index <= values.Length - 1 _
            AndAlso to_index >= 0 AndAlso to_index <= values.Length - 1 Then

            For i As Integer = from_index To to_index
                tot += values(i)
            Next

        Else
            Throw New ArgumentOutOfRangeException
        End If

        Return tot

    End Function
#End Region


#Region "STATUS"

    Public Class GSstatus
        ''' <summary>
        ''' Generation counter
        ''' </summary>
        Public generation As Integer = 0

        Public maximising As Boolean = False

        ''' <summary>
        ''' Fitness of the best individual
        ''' </summary>
        Public Property bestFitness As Double
            Get
                Return _bestHistory(_h)
            End Get
            Set(value As Double)
                _h += 1
                If _h = _HS Then _h -= _HS
                _bestHistory(_h) = value
            End Set
        End Property

        Public ReadOnly Property Gap As Double
            Get
                Dim prev As Integer = _h - 1
                If prev < 0 Then prev += _HS
                Return (_bestHistory(_h) - _bestHistory(prev)) / _bestHistory(prev)
            End Get
        End Property

        Friend Sub initHistory(maxGenWithoutImprovement As Integer)
            _HS = maxGenWithoutImprovement
            ReDim _bestHistory(_HS - 1)
            _h = _HS - 1
        End Sub

        ''' <summary>
        ''' Fitness History size
        ''' </summary>
        Private _HS As Integer
        ''' <summary>
        ''' Circular Array with historical best fitness values
        ''' </summary>
        Private _bestHistory() As Double
        ''' <summary>
        ''' Index of the last fitness value written
        ''' </summary>
        Private _h As Integer

        Public ReadOnly Property NoImprovements As Boolean
            Get
                Dim prev As Integer = _h - 1
                If prev < 0 Then prev += _HS
                While _bestHistory(prev) = _bestHistory(_h)
                    prev -= 1
                    If prev < 0 Then prev += _HS
                    'if the whole history has been scanned without finding a different value then tey're all the same
                    If prev = _h Then
                        Return True
                    End If
                End While

                'if at any point a different value is found then there must have been changes in the last HS iterations
                Return False

            End Get
        End Property


        Public mutationGain As Double = 1

        ''' <summary>
        ''' Average population fitness
        ''' </summary>
        Public averageFitness As Double

    End Class

    Public Enum BandwidthType
        OneWayHard = 0
        TwoWayHard = 1
        TwoWaySlack = 2
    End Enum
#End Region

#Region "SETTINGS"
    Public Class GSparameters
        Public fitnessThreshold As Double = 0.01
        Public maxGenerations As Integer = 3000
        Public maxGenWithoutImprovement As Integer = 1500
        Public populationSize As Integer = 1000

        Public thresholdUpdateMethod As Integer = 0
        Public thresholdPercentile As Double = 0.2

        Public mutationRate As Double = 0.3
        Public maxMutationGain As Double = 2
        Public crossoverPoints As Integer = 1

        Public bandwidthType As BandwidthType = BandwidthType.TwoWaySlack
        Public secondaryBandWeight As Double = 0.5
    End Class
#End Region

End Class
