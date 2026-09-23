using UnityEngine;

namespace HallLab
{
    public sealed class StageMechanics : MonoBehaviour
    {
        public Transform xSaddle, xThimble, yThimble;
        public void SetPose(float x,float y)
        {
            xSaddle.localPosition=new Vector3(x*.018f,.036f,0);
            // Turns are illustrative: the physical screw pitch has not been measured.
            xThimble.localRotation=Quaternion.Euler(x*270,0,0);
            yThimble.localRotation=Quaternion.Euler(0,0,y*270);
        }
    }
}
