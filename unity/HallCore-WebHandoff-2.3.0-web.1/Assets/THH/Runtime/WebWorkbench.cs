namespace HallLab
{
    public sealed partial class THHWorkbench
    {
        public void WebNewExperiment()
        {
            ExperimentModel=new HallExperimentModel();ResetControls();renderedVersion=-1;ShowTab(0);
        }
        public void WebMoveStage(string axis,float value) {MoveStage(axis=="stageX"?value:stageX,axis=="stageY"?value:stageY);}
    }
}
