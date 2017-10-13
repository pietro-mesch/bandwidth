Module m_main

    Sub Main()
        Console.SetWindowSize(125, 40)
        'test loop iterations
        Dim nIte As Integer = 100
        'test number of junctions
        Dim n As Integer = 5
        ReDim corridor(1)

        Dim view As CorridorViewForm

        'TESTING GENETIC
        Dim ga As GeneticSynchroniser
        'random corridor
        corridor(1) = New t_CORRIDOR(n)
        For i As Integer = 1 To nIte
            ga = New GeneticSynchroniser(corridor(1))
            ga.Optimize()
            view = New CorridorViewForm(1)
            view.Show()
            view.Draw()
            view.Close()
        Next


        Dim opt As BandMaximiser
        Dim r As New Random

        For i As Integer = 1 To nIte
            ReDim corridor(1)
            'random corridor
            corridor(1) = New t_CORRIDOR(n)

            opt = New BandMaximiser(corridor(1))

            corridor(1).offs = opt.GetOffsetsTwoWayBand(0).Select(Function(x) CInt(Math.Round(x))).ToArray
            view = New CorridorViewForm(1)
            view.Show()
            view.Draw()
            view.Close()

            corridor(1).offs = opt.GetOffsetsTwoWayBand(0.5).Select(Function(x) CInt(Math.Round(x))).ToArray
            view = New CorridorViewForm(1)
            view.Show()
            view.Draw()
            view.Close()

            corridor(1).offs = opt.GetOffsetsTwoWayBand(1).Select(Function(x) CInt(Math.Round(x))).ToArray
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
    Public corridor As t_CORRIDOR()

    'PM REM THIS duplicates minput
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
