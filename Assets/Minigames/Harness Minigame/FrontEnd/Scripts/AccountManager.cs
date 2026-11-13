using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// Simple local accounts (PlayerPrefs) so login / sign-up work now.
// Later: swap the inside of SignUp / Login for Firebase calls - the UI doesn't need to change.
public static class AccountManager
{
    [Serializable] public class Account { public string name; public string email; }
    [Serializable] class AccountList { public List<Account> accounts = new List<Account>(); }

    const string KEY = "nyp_oh_accounts";
    const string CURRENT = "nyp_oh_current_email";

    public static Account Current { get; private set; }
    public static bool IsGuest { get; private set; }
    public static bool LoggedIn => Current != null;
    public static string DisplayName => LoggedIn ? Current.name : "Guest";

    static AccountList Load()
    {
        var json = PlayerPrefs.GetString(KEY, "");
        return string.IsNullOrEmpty(json) ? new AccountList() : JsonUtility.FromJson<AccountList>(json);
    }

    static void Save(AccountList list)
    {
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }

    public static bool ValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    // returns null if OK, otherwise an error message to show
    public static string SignUp(string name, string email)
    {
        name = (name ?? "").Trim(); email = (email ?? "").Trim().ToLower();
        if (name.Length == 0) return "Please enter your name.";
        if (!ValidEmail(email)) return "Please enter a valid email.";
        var list = Load();
        if (list.accounts.Exists(a => a.email == email)) return "That email already has an account. Log in instead.";
        var acc = new Account { name = name, email = email };
        list.accounts.Add(acc);
        Save(list);
        SetCurrent(acc);
        return null;
    }

    public static string Login(string email)
    {
        email = (email ?? "").Trim().ToLower();
        if (!ValidEmail(email)) return "Please enter a valid email.";
        var acc = Load().accounts.Find(a => a.email == email);
        if (acc == null) return "No account with that email yet. Play as guest - you can sign up after your first quest.";
        SetCurrent(acc);
        return null;
    }

    // Used after Firebase confirms the account: remember it on this device too
    public static void UseAccount(string name, string email)
    {
        email = (email ?? "").Trim().ToLower();
        var list = Load();
        var acc = list.accounts.Find(a => a.email == email);
        if (acc == null) { acc = new Account { name = name, email = email }; list.accounts.Add(acc); }
        else acc.name = name;
        Save(list);
        SetCurrent(acc);
    }

    public static void PlayAsGuest()
    {
        Current = null; IsGuest = true;
        PlayerPrefs.DeleteKey(CURRENT);
    }

    public static void LogOut()
    {
        Current = null; IsGuest = false;
        PlayerPrefs.DeleteKey(CURRENT);
    }

    // remembers the last logged-in player on this browser/device
    public static void RestoreSession()
    {
        var email = PlayerPrefs.GetString(CURRENT, "");
        if (email.Length == 0) return;
        var acc = Load().accounts.Find(a => a.email == email);
        if (acc != null) SetCurrent(acc);
    }

    static void SetCurrent(Account acc)
    {
        Current = acc; IsGuest = false;
        PlayerPrefs.SetString(CURRENT, acc.email);
        PlayerPrefs.Save();
    }
}
