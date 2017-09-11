Module m_main

    Sub Main()

        'test loop iterations
        Dim nIte As Integer = 10
        'test number of junctions
        Dim n As Integer = 10
        Dim cycl As Integer = 120
        Dim trav(n - 2) As Double
        Dim trav2(n - 2) As Double
        Dim gini(n - 1) As Double
        Dim gini2(n - 1) As Double
        Dim gend(n - 1) As Double
        Dim gend2(n - 1) As Double

        Dim opt As BandMaximiser
        Dim r As New Random
        Dim offs As Double()

        For i As Integer = 1 To nIte
            For j As Integer = 0 To n - 1
                'travel times
                If j < n - 1 Then
                    trav(j) = r.Next(30, 130)
                    trav2(j) = trav(j) * 0.8
                End If

                gini(j) = r.Next(0, cycl + 1)
                gend(j) = gini(j) + r.Next(30, 70)
                If gend(j) > cycl Then gend(j) -= cycl

                gini2(j) = gini(j) + r.Next(0, cycl + 1)
                gend2(j) = gend(j) + gini2(j) - gini(j) + r.Next(0, 10) - 5
                If gini2(j) > cycl Then gini2(j) -= cycl
                If gend2(j) > cycl Then gend2(j) -= cycl

            Next

            opt = New BandMaximiser(cycl, trav, gini, gend, trav2:=trav2)
            offs = opt.OneWayOffsets()
            offs = opt.TwoWayOffsets()
            opt = New BandMaximiser(cycl, trav, gini, gend, gini2:=gini2, gend2:=gend2)
            offs = opt.TwoWayOffsets(0)

        Next


#If DEBUG Then
        Stop
#End If
    End Sub

End Module
