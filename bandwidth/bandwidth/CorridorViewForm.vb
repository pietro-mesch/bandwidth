Public Class CorridorViewForm
    Private _corridorViewer As CorridorViewer

    Public Sub New(ByVal corridorIndex As Integer)

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

        _corridorViewer = New CorridorViewer(corridorIndex, PictureBox1)

        Label1.Text = String.Format("Corridor {0} : {1}", corridor(corridorIndex).idno, corridor(corridorIndex).name)

    End Sub

    Public Sub Draw()
        _corridorViewer.Draw()
    End Sub
End Class