using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
   public FirebaseNetwork network;
   public GameObject signInMenu,setUsernameMenu;
   
   public TextMeshProUGUI isAvailableText;
   public Button confirmButton;
   public TMP_InputField inputUsername;

   private void Start()
   {
      inputUsername.onValueChanged.AddListener(CheckUsernameListener);
   }

   private void CheckUsernameListener(string text)
   {
      if (text.Trim().Length > 2 && text.Trim().Length < 12)
      {
         isAvailableText.gameObject.SetActive(true);
         confirmButton.interactable = true;
         network.CheckUsername(text, isAvailableText, confirmButton);
      }
      else
      {
         isAvailableText.gameObject.SetActive(true);
         isAvailableText.text = "Username must be between 2 and 12";
         isAvailableText.color = Color.red;
         confirmButton.interactable = false;
      }
   }

   public void ButtonsManager()
   {
      if (!network.CurrentUser())
      {
         signInMenu.SetActive(true);
      }
      else
      {
         Debug.Log("Kullanıcı var");
      }
   }
   public void SignInWithGoogle() => network.SignInWithGoogle();
   
   public void SignInAnonymous() => network.SignInAnonymous();
   public void OpenSetUNameMenu(string uName, int signInMethod)
   {
      Debug.Log("çalıştı");
      signInMenu.SetActive(false);
      setUsernameMenu.SetActive(true);
      
      inputUsername.text = uName;
      inputUsername.enabled = signInMethod == 2;
      
      if (signInMethod == 1) //Anon ise
         confirmButton.interactable = true;
   }
}
