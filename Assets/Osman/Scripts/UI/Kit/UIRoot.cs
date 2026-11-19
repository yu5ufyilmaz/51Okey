using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Sahneler arası kalıcı UI katmanı: bildirim, yükleme, onay penceresi ve Android geri tuşu.
// İlk statik çağrıda Resources/UIRoot prefab'ından kurulur; play modu dışında ve kapanırken hiçbir şey yapmaz.
public class UIRoot : MonoBehaviour
{
    const float ToastSeconds = 2.5f;

    [SerializeField] GameObject toast;
    [SerializeField] TMP_Text toastText;
    [SerializeField] GameObject loading;
    [SerializeField] TMP_Text loadingText;
    [SerializeField] RectTransform ring;
    [SerializeField] UiWindow confirm;
    [SerializeField] TMP_Text confirmTitle;
    [SerializeField] TMP_Text confirmBody;
    [SerializeField] Button yes;
    [SerializeField] Button yesDestructive;
    [SerializeField] Button no;
    [SerializeField] Button close;

    static UIRoot inst;
    static bool quitting;

    readonly UiBackStack<UiWindow> stack = new UiBackStack<UiWindow>();
    readonly UiLoadingTimer timer = new UiLoadingTimer();
    TaskCompletionSource<bool> pending;
    float toastUntil;
    bool askExitAfterBack;
    bool raiseConfirm;  // takılan yüklemede açılan onay yükleme katmanının üstüne çıkar
    bool confirmRaised;

    // Domain reload kapalıyken play'e girişte statikler sıfırlanmalı
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        inst = null;
        quitting = false;
        Application.quitting -= OnQuit;
        Application.quitting += OnQuit;
    }

    static void OnQuit() => quitting = true;

    // Geri tuşu açılıştan itibaren çalışsın; Update ancak örnek varken döner
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => _ = I;

    static UIRoot I
    {
        get
        {
            if (inst == null && Application.isPlaying && !quitting)
            {
                var prefab = Resources.Load<UIRoot>("UIRoot");
                if (prefab != null) Instantiate(prefab); // Awake inst'i atar
                else Debug.LogWarning("[UIRoot] Resources/UIRoot prefab'ı yok.");
            }
            return inst;
        }
    }

    public static bool IsLoading => inst != null && inst.timer.Active;

    public static void Toast(string text)
    {
        var r = I;
        if (r == null) return;
        r.toastText.text = text;
        r.toast.SetActive(true);
        r.toastUntil = Time.unscaledTime + ToastSeconds;
    }

    // text null iken açık katmanın metni korunur; her çağrı sayacı yeniden başlatır.
    public static void ShowLoading(string text = null, bool timeout = true)
    {
        var r = I;
        if (r == null) return;
        if (text != null || !r.loading.activeSelf) r.loadingText.text = text ?? "";
        r.loading.SetActive(true);
        r.timer.Show(Time.unscaledTime, timeout);
    }

    // Sahne geçişi açık yüklemenin metnini ve zaman aşımı kipini bozmasın; kapalıysa ShowLoading gibi açılır.
    public static void KeepLoading()
    {
        if (!IsLoading) ShowLoading();
    }

    public static void HideLoading()
    {
        if (inst == null) return;
        inst.timer.Hide();
        inst.loading.SetActive(false);
    }

    public static Task<bool> Confirm(string title, string body, string yesLabel = "Evet", bool destructive = false)
    {
        var r = I;
        if (r == null) return Task.FromResult(false);
        r.Resolve(false); // açık onay varsa eskisi hayır sayılır
        r.pending = new TaskCompletionSource<bool>();
        r.confirmTitle.text = title;
        r.confirmBody.text = body;
        var b = destructive ? r.yesDestructive : r.yes;
        b.GetComponentInChildren<TMP_Text>(true).text = yesLabel;
        r.yes.gameObject.SetActive(!destructive);
        r.yesDestructive.gameObject.SetActive(destructive);
        r.confirm.gameObject.SetActive(true);
        if (r.raiseConfirm)
        {
            r.confirm.transform.SetAsLastSibling();
            r.toast.transform.SetAsLastSibling();
            r.confirmRaised = true;
        }
        return r.pending.Task;
    }

    public static void Register(UiWindow w)
    {
        var r = I;
        if (r != null) r.stack.Push(w);
    }

    public static void Unregister(UiWindow w)
    {
        if (inst != null) inst.stack.Remove(w);
    }

    // Geri tuşunun yaptığı iş; Update Escape'te çağırır.
    public static void HandleBack()
    {
        if (inst == null) return;
        // Zaman aşımsız yükleme takılabilir: geri tuşu çıkış sorar, pencere kapatmaz
        if (IsLoading && !inst.timer.TimeoutOn && inst.pending == null)
        {
            inst.askExitAfterBack = false;
            inst.raiseConfirm = true;
            inst.AskExit(); // Confirm ilk await'ten önce eşzamanlı açılır
            inst.raiseConfirm = false;
            return;
        }
        var res = inst.stack.Back(IsLoading && inst.timer.TimeoutOn, w => w != null && w.isActiveAndEnabled, inst.CloseWindow);
        if ((res == BackResult.AskExit || inst.askExitAfterBack) && inst.pending == null)
            inst.AskExit();
        inst.askExitAfterBack = false;
    }

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(gameObject); return; }
        inst = this;
        DontDestroyOnLoad(gameObject);
        yes.onClick.AddListener(() => Resolve(true));
        yesDestructive.onClick.AddListener(() => Resolve(true));
        no.onClick.AddListener(() => Resolve(false));
        close.onClick.AddListener(() => Resolve(false));
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    // Sahne değişince açık onay hayır sayılır (çağıran sahne nesnesi artık yok). Bildirim ve yükleme kalır.
    void OnSceneChanged(Scene from, Scene to) => Resolve(false);

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        if (inst == this) inst = null; // devamı Toast/ShowLoading çağırırsa yok edilen nesneye dokunmasın
        Resolve(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleBack();
        if (timer.Tick(Time.unscaledTime))
        {
            HideLoading();
            Toast("Bağlantı zaman aşımına uğradı");
        }
        if (toast.activeSelf && Time.unscaledTime >= toastUntil)
            toast.SetActive(false);
        if (loading.activeSelf)
            ring.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
    }

    void CloseWindow(UiWindow w)
    {
        if (!w.closable) { stack.Push(w); askExitAfterBack = true; return; } // pencere yığında kalır, çıkış sorulur
        if (w == confirm) { Resolve(false); return; }
        w.Close();
    }

    // Pencere önce kapanır, sonra sonuç verilir; devamında yeni Confirm açılırsa kapanmasın
    void Resolve(bool value)
    {
        var p = pending;
        pending = null;
        if (confirm != null) confirm.gameObject.SetActive(false); // yok edilirken çocuk gitmiş olabilir
        if (confirmRaised && confirm != null) confirm.transform.SetSiblingIndex(loading.transform.GetSiblingIndex()); // normal sıra: Confirm, Loading, Toast
        confirmRaised = false;
        p?.TrySetResult(value);
    }

    async void AskExit()
    {
        if (SceneManager.GetActiveScene().name == "Table")
        {
            var exit = FindFirstObjectByType<ExitButton>();
            if (exit != null) exit.AskExit(); // onay metni ExitButton'da
        }
        else if (await Confirm("Çıkış", "Oyundan çıkılsın mı?", "Çık", true))
            Application.Quit();
    }
}
