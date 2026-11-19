using System;
using System.Collections.Generic;

public enum BackResult { Swallowed, ClosedWindow, AskExit }

// Android geri tuşu için açık pencere yığını. Sahneden bağımsız saf mantık, test edilir.
public class UiBackStack<T> where T : class
{
    readonly List<T> stack = new List<T>();

    public int Count => stack.Count;

    public void Push(T w)
    {
        stack.Remove(w); // aynı pencere iki kez girmesin
        stack.Add(w);
    }

    public void Remove(T w) => stack.Remove(w);

    // Yükleme sürerken tuş yutulur; yoksa en üstteki canlı pencere kapanır, hiç yoksa çıkış sorulur.
    public BackResult Back(bool loading, Func<T, bool> alive, Action<T> close)
    {
        if (loading)
            return BackResult.Swallowed;
        while (stack.Count > 0)
        {
            T top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            if (!alive(top))
                continue;
            close(top);
            return BackResult.ClosedWindow;
        }
        return BackResult.AskExit;
    }
}
