using System.IO;
using UnityEngine;
using Unity.MLAgents;

public class TrainingLogger : MonoBehaviour
{
    private string sessionID;
    private string csvPath;
    public string logFileName = "NoObstacle";

    void Start()
    {
        sessionID = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        Directory.CreateDirectory(Application.dataPath + "/TrainingLogs");
        // csvPath = Application.dataPath + $"/TrainingLogs/{sessionID}.csv";
        // File.WriteAllText(csvPath, "Timestamp,Episode,Step,Speed,Reward,Steering,Accel\n");
    }

    // public void LogData(int episode, int step, float speed, float reward, float steering, float accel)
    // {
    //     string entry = $"{Time.time:F2},{episode},{step}," +
    //                   $"{speed:F2},{reward:F4}," +
    //                   $"{steering:F2},{accel:F2}\n";
    //     File.AppendAllText(csvPath, entry);
    // }

//     public void LogRayRewards(int step, float time, float[] rayRewards)
// {
//     using (StreamWriter writer = new StreamWriter("RayRewardsLog.csv", true))
//     {
//         string rewardsString = string.Join(",", rayRewards);
//         writer.WriteLine($"{time},{step},{rewardsString}");
//     }
// }

public void LogEpisodeEnd(int episode, float duration,float reward ,string reason)
{
    string path = Application.dataPath + $"/TrainingLogs/{logFileName}.csv";
    if (!File.Exists(path))
        File.WriteAllText(path, "Timestamp,Episode,Duration,Reward,Reason\n");

    string entry = $"{System.DateTime.Now:HH:mm:ss},{episode},{duration:F2},{reward},{reason}\n";
    File.AppendAllText(path, entry);
}

}