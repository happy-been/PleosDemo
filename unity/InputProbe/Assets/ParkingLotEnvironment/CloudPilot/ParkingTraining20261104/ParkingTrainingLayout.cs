using System;using UnityEngine;
namespace ParkingLotEnvironment.Training {
 public enum ParkingManeuver { ReversePerpendicular, Parallel }
 [Serializable] public class PracticeBay {public string id;public ParkingManeuver maneuver;public Vector3 center;public float width,length;public float headingDegrees;public string surroundingVehicles;public Transform approach;}
 [Serializable] public class ParkedObstacle {public string id,category,model;public Vector3 center,size;}
 public sealed class ParkingTrainingLayout:MonoBehaviour {
  public PracticeBay[] bays;public ParkedObstacle[] obstacles;public Transform entrySpawn;public Vector2 drivingX=new Vector2(-2.9f,5.25f);public Vector2 drivingZ=new Vector2(-20,14);
  public string truckModelNote="Existing generic cab-over mesh adapted to Porter-class dimensions; not a licensed Hyundai Porter model.";
  public PracticeBay Find(string id){if(bays!=null)foreach(var bay in bays)if(bay.id==id)return bay;return null;}
  void OnDrawGizmosSelected(){if(bays==null)return;foreach(var bay in bays){Gizmos.color=bay.maneuver==ParkingManeuver.Parallel?Color.cyan:Color.green;var m=Gizmos.matrix;Gizmos.matrix=Matrix4x4.TRS(bay.center,Quaternion.Euler(0,bay.headingDegrees,0),Vector3.one);Gizmos.DrawWireCube(Vector3.zero,new Vector3(bay.width,.05f,bay.length));Gizmos.matrix=m;}}
 }
}
