using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;

// Auth ve Firestore aynı FirebaseApp'i paylaşır. Bağımlılık kontrolü ilk kullanımdan önce; hata önbelleğe alınmaz.
// Çağrılar ana iş parçacığından await edilir: UnitySynchronizationContext devamı ana iş parçacığına döndürür.
public class FirebaseBoot
{
    readonly string instanceTag;
    readonly bool isEditor;
    Task<FirebaseApp> app;

    public FirebaseBoot(string instanceTag, bool isEditor)
    {
        this.instanceTag = instanceTag;
        this.isEditor = isEditor;
    }

    public Task<FirebaseApp> AppAsync()
    {
        if (app == null || app.IsFaulted || app.IsCanceled)
            app = CreateAsync();
        return app;
    }

    public async Task<FirebaseAuth> AuthAsync() => FirebaseAuth.GetAuth(await AppAsync());

    public async Task<FirebaseFirestore> FirestoreAsync() => FirebaseFirestore.GetInstance(await AppAsync());

    async Task<FirebaseApp> CreateAsync()
    {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available)
            throw new InvalidOperationException("Firebase kullanılamıyor: " + status);
        if (!isEditor)
            return FirebaseApp.DefaultInstance;
        // Editör kopyaları ayrı anonim kullanıcı tutsun (tasarım 5.1): klasör adıyla ayrı uygulama.
        return FirebaseApp.GetInstance(instanceTag) ?? FirebaseApp.Create(FirebaseApp.DefaultInstance.Options, instanceTag);
    }
}
