using UnityEngine;
using Firebase;
using Firebase.Extensions;

public class FirebaseInit : MonoBehaviour
{
    private void Start()
    {
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                // Firebase is ready to use.
                Debug.Log("Firebase is ready!");
            }
            else
            {
                Debug.LogError(System.String.Format(
                    "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
            }
        });
    }
}