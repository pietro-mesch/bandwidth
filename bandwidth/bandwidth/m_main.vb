Module m_main

    Sub Main()

        'test loop iterations
        Dim nIte As Integer = 10
        'test number of junctions
        Dim n As Integer = 10
        Dim cycl As Integer = 120
        Dim trav(n - 2) As Double
        Dim gini(n - 1) As Double
        Dim gend(n - 1) As Double

        Dim opt As BandMaximiser
        Dim r As New Random
        Dim offs As Double()

        For i As Integer = 1 To nIte
            For j As Integer = 0 To n - 1
                If j < n - 1 Then trav(j) = r.Next(30, 130)
                gini(j) = r.Next(0, cycl + 1)
                gend(j) = gini(j) + r.Next(30, 70)
                If gend(j) > cycl Then gend(j) -= cycl
            Next

            opt = New BandMaximiser(cycl, trav, gini, gend)
            offs = opt.OneWayOffsets()
        Next


#If DEBUG Then
        Stop
#End If
    End Sub

End Module
