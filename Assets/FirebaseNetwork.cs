using System;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.UI;
using Google;
using TMPro;
using UnityEngine.Rendering;


public class FirebaseNetwork : MonoBehaviour
{
    private string WebAPI = "146708517675-vf1e66haqk77sj8nngrhjgufbr7v7pp8.apps.googleusercontent.com";
    public MenuManager menuManager;
    FirebaseAuth auth;
    FirebaseUser fuser;
    public Button defaultButton;
    DatabaseReference reference;
    GoogleSignInConfiguration configuration;

    private void Awake()
    {
        configuration = new GoogleSignInConfiguration
        {
            WebClientId = WebAPI,
            RequestIdToken = true
        };
    }

    private void Start()
    {
        InitializeFirebase();
      
    }

    private AudioClip microphoneClip;
    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.Escape))
        {
            auth.SignOut();
            fuser = null;
            Debug.Log("esc bastın");
        }

      
        
    }

    public bool CurrentUser()
    {
        return fuser != null;
    }
    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            DependencyStatus dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                fuser = auth.CurrentUser;
                defaultButton.interactable = true;
                reference = FirebaseDatabase.DefaultInstance.RootReference.Child("Users");
                 Debug.Log("Firebase initialized");
                // reference.Child("User1").Child("Name").SetValueAsync("Yusuf");
                // reference.Child("User1").Child("Age").SetValueAsync("23");
                //
                // reference.Child("User2").Child("Name").SetValueAsync("Yasir");
                // reference.Child("User2").Child("Age").SetValueAsync("13");
                //
                // DataSnapshot snapshot = reference.GetValueAsync().Result;
                // Debug.Log(snapshot.Child("User2").Child("Name").Value);
            }
            else
            {
                defaultButton.interactable = false;
            }
        
        });
    }

    public void SignInAnonymous()
    {
        
        Debug.Log("FirebaseNetwork - SignInAnonymous Called");
        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("SignInAnonymous");
            }
            else
            {
               AuthResult authResult = task.Result;
               fuser = authResult.User;

               reference.GetValueAsync().ContinueWithOnMainThread(vTask =>
               {
                   if (vTask.IsFaulted || vTask.IsCanceled)
                   {
                       Debug.LogError("Error A");
                       return;
                   }
                   DataSnapshot snapshot = vTask.Result;
                   Debug.Log("Anon user is signed in");
                   string username = "user_" + (snapshot.ChildrenCount + 1);
                   menuManager.OpenSetUNameMenu( username ,1);
                   reference.Child(fuser.UserId).Child("Username").SetValueAsync(username).ContinueWithOnMainThread(task =>
                   {
                       if (task.IsFaulted || task.IsCanceled)
                           return;
                       // Yazma işlemi tamamlanınca gerçekleşecekler
                   } );
               });
            }
           
        });
    }

    public void SignInWithGoogle()
    {
        GoogleSignIn.Configuration = configuration;
        GoogleSignIn.Configuration.UseGameSignIn = false;
        GoogleSignIn.Configuration.RequestIdToken = true;
        GoogleSignIn.Configuration.RequestEmail = true;
        GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(FinishSignIn);
    }

    private void FinishSignIn(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            Debug.LogError(task.Exception);
        }
        else
        {
            Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
            auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError(task.Exception);
                }
                
                fuser = auth.CurrentUser;
            });
        }
    }
    
    public void CheckUsername(string text, TextMeshProUGUI isAvailableText, Button confirmButton)
    {
        reference.OrderByChild("Username").EqualTo(text).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError(task.Exception);
                return;
            }
           
            DataSnapshot snapshot = task.Result;
            if (snapshot.HasChildren)
            {
                Debug.Log(snapshot.Child("Username"));
                isAvailableText.text = "This username is not available";
                isAvailableText.color = Color.red;
                confirmButton.interactable = false;
            }
            else
            {
                Debug.Log("Müsaaait");
                isAvailableText.text = "This username is available";
                isAvailableText.color = Color.green;
                confirmButton.interactable = true;
            }
        });
    }
}