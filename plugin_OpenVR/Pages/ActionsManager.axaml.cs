using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Amethyst.Contract;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace plugin_OpenVR.Pages;

public partial class ActionsManager : UserControl, INotifyPropertyChanged
{
    private bool _listViewChangeBlock = false;
    public new event PropertyChangedEventHandler PropertyChanged;

    public bool IsAddingNewActionInverse
    {
        get => !IsAddingNewAction;
    }

    public ActionsManager()
    {
        InitializeComponent();
    }

    public IAmethystHost Host { get; set; }
    public SteamVR DataParent { get; set; }
    private InputAction TreeSelectedAction { get; set; }
    public bool IsAddingNewAction { get; set; }

    public string SelectedActionName
    {
        get => IsAddingNewAction && TreeSelectedAction is not null ?
            NewActionName :
            TreeSelectedAction?.NameLocalized ?? Host?.RequestLocalizedString("/InputActions/Title/NoSelection") ?? "";
        set
        {
            if (!IsAddingNewAction || TreeSelectedAction is null) return;
            NewActionName = value;
            OnPropertyChanged();
        }
    }

    public IEnumerable<InputAction> CustomActions
    {
        get => DataParent?.VrInput?.RegisteredActions?.Actions?.Where(x => x?.Custom ?? false) ?? [];
    }

    public string SelectedActionCode
    {
        get => TreeSelectedAction?.Code ?? string.Empty;
        set
        {
            if (TreeSelectedAction is null) return;
            TreeSelectedAction.Code = value;
        }
    }

    public string SelectedActionDescription
    {
        get => TreeSelectedAction?.Name ?? string.Empty;
    }

    public bool SelectedActionValid
    {
        get => TreeSelectedAction?.Valid ?? false;
    }

    public double SelectedActionValidOpacity
    {
        get => SelectedActionValid ? 1.0 : 0.0;
    }

    public bool SelectedActionInvalid
    {
        get => !SelectedActionValid;
    }

    public bool ActionValid
    {
        get => TreeSelectedAction is not null && (!IsAddingNewAction || !string.IsNullOrEmpty(SelectedActionName));
    }

    public string NewActionName { get; set; }

    private async void ActionTestButton_OnClick(object o, RoutedEventArgs routedEventArgs)
    {
        try
        {
            if (!TestResultsBox.IsLoaded || TreeSelectedAction is null) return;
            TestResultsBox.Text = await TreeSelectedAction.Invoke(null);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void RemoveAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TestResultsBox.IsLoaded || TreeSelectedAction is null) return;
        DataParent.VrInput.RegisteredActions.Actions.Remove(TreeSelectedAction);
        DataParent.VrInput.SaveSettings();

        TreeSelectedAction = null;
        ActionRemoveSplitButton?.Flyout?.Hide();

        OnPropertyChanged();
    }

    private void ActionsListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox || _listViewChangeBlock) return;
        if (e.AddedItems.Count < 1 || e.AddedItems[0] is not InputAction action)
        {
            Host?.PlayAppSound(SoundType.Focus);
            return; // Give up now...
        }

        var shouldAnimate = TreeSelectedAction != action;
        TreeSelectedAction = action;
        IsAddingNewAction = false;
        OnPropertyChanged();

        if (!shouldAnimate) return;
        Host?.PlayAppSound(SoundType.Invoke);
    }

    private void NewActionItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ActionsListView.IsLoaded) return;
        ActionsListView.SelectedItem = null;

        TreeSelectedAction = new InputAction(
            $"/actions/default/in/{Guid.NewGuid().ToString().ToUpper()}",
            "boolean", "optional");

        NewActionName = string.Empty;
        IsAddingNewAction = true;

        Host?.PlayAppSound(SoundType.Invoke);
        OnPropertyChanged();
    }
    
    private void ReloadActions()
    {
        TreeSelectedAction = null;
        IsAddingNewAction = !CustomActions.Any();
        if (IsAddingNewAction)
        {
            TreeSelectedAction = new InputAction(
                $"/actions/default/in/{Guid.NewGuid().ToString().ToUpper()}",
                "boolean", "optional");

            NewActionName = string.Empty;
        }

        ActionsListView.SelectedItem = null;
        OnPropertyChanged();
    }

    private void OnPropertyChanged(string propertyName = null)
    {
        _listViewChangeBlock = true;
        var itemBackup = ActionsListView.SelectedItem;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (ActionsListView.Items.Contains(itemBackup))
            ActionsListView.SelectedItem = itemBackup;

        _listViewChangeBlock = false;
    }

    private void AddNewAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (!((sender as Button)?.IsLoaded ?? false)) return;

        DataParent.VrInput.RegisteredActions.Actions.Add(TreeSelectedAction);
        TreeSelectedAction.NameLocalized = NewActionName;
        DataParent.VrInput.SaveSettings();
        DataParent.VrInput.InitInputActions();

        IsAddingNewAction = false;
        Host?.PlayAppSound(SoundType.Invoke);

        ReloadActions();

        if (ActionsListView.Items.Any())
            ActionsListView.SelectedItem = ActionsListView.Items[^1];
    }

}
