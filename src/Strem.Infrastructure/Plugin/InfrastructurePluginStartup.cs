﻿using System.IO.Compression;
using System.Reactive.Disposables;
using LiteDB;
using Strem.Core.Events;
using Strem.Core.Events.Bus;
using Strem.Core.Extensions;
using Strem.Core.Plugins;
using Strem.Core.Services.Registries.Integrations;
using Strem.Core.Services.Registries.Menus;
using Strem.Core.State;
using Strem.Core.Variables;
using Strem.Data.Types;
using Strem.Infrastructure.Extensions;
using Strem.Infrastructure.Services;
using Strem.Infrastructure.Services.Persistence;

namespace Strem.Infrastructure.Plugin;

public class InfrastructurePluginStartup : IPluginStartup, IDisposable
{
    private readonly CompositeDisposable _subs = new();
    
    public IAppState AppState { get; }
    public IEventBus EventBus { get; }
    public IAppVariablesRepository AppVariablesRepository { get; }
    public IUserVariablesRepository UserVariablesRepository { get; }
    public ILogger<InfrastructurePluginStartup> Logger { get; }
    public ILiteDatabase Database { get; }
    public IIntegrationRegistry IntegrationRegistry { get; }
    public IMenuRegistry MenuRegistry { get; }
    public IServiceProvider Services { get; }

    public InfrastructurePluginStartup(IAppState appState, IEventBus eventBus, IAppVariablesRepository appVariablesRepository, IUserVariablesRepository userVariablesRepository, ILogger<InfrastructurePluginStartup> logger, ILiteDatabase database, IIntegrationRegistry integrationRegistry, IMenuRegistry menuRegistry, IServiceProvider services)
    {
        AppState = appState;
        EventBus = eventBus;
        AppVariablesRepository = appVariablesRepository;
        UserVariablesRepository = userVariablesRepository;
        Logger = logger;
        Database = database;
        IntegrationRegistry = integrationRegistry;
        MenuRegistry = menuRegistry;
        Services = services;
    }

    public async Task StartPlugin()
    {
        await CheckIfBackupIsNeeded();
        
        AppState.UserVariables.OnVariableChanged
            .Subscribe(x =>
            {
                if (AppState.UserVariables.Has(x.Key))
                { UserVariablesRepository.Upsert(x.Key, x); }
                else
                { UserVariablesRepository.Delete(x.Key); }
            })
            .AddTo(_subs);

        AppState.AppVariables.OnVariableChanged
            .Subscribe(x =>
            {
                if (AppState.AppVariables.Has(x.Key))
                { AppVariablesRepository.Upsert(x.Key, x); }
                else
                { AppVariablesRepository.Delete(x.Key); }
            })
            .AddTo(_subs);
        
        AppState.UserVariables.OnVariableChanged
            .Subscribe(x => Logger.Information($"User Variables [{x.Key.Name}|{x.Key.Context}] Changed"))
            .AddTo(_subs);

        AppState.AppVariables.OnVariableChanged
            .Subscribe(x => Logger.Information($"App Variables [{x.Key.Name}|{x.Key.Context}] Changed"))
            .AddTo(_subs);

        EventBus.Receive<ErrorEvent>().Subscribe(x => Logger.Error($"[{x.Source}]: {x.Message}"));

        Logger.Information("Setting Up Integration Registries");
        SetupRegistries();
        Logger.Information("Setup Integration Registries");
        
        SetDefaultSettingsIfNotSet();
    }
    
    public void SetDefaultSettingsIfNotSet()
    {
        if(!AppState.AppVariables.Has(UIVariables.ShowHelpersVariable))
        { AppState.AppVariables.Set(UIVariables.ShowHelpersVariable, true); }
        
        if(!AppState.AppVariables.Has(UIVariables.ZoomVariable))
        { AppState.AppVariables.Set(UIVariables.ZoomVariable, 100); }
    }

    public async Task CheckIfBackupIsNeeded()
    {
        var lastBackupDate = DateTime.MinValue;
        if (AppState.AppVariables.Has(UIVariables.LastBackupDate))
        { DateTime.TryParse(AppState.AppVariables.Get(UIVariables.LastBackupDate), out lastBackupDate); }

        var backupIntervalInDays = 7;
        if (AppState.AppVariables.Has(UIVariables.BackupFrequencyInDays))
        {
            var requestedFrequency = AppState.AppVariables.Get<int>(UIVariables.BackupFrequencyInDays);
            if(requestedFrequency > 0)
            { backupIntervalInDays = requestedFrequency; }
        }

        var timeSinceLastBackup = DateTime.Now - lastBackupDate ;
        if(timeSinceLastBackup.TotalDays < backupIntervalInDays) { return; }

        Logger.Information("Making a backup of app related data");
        await BackupFiles();
        AppState.AppVariables.Set(UIVariables.LastBackupDate, DateTime.Now.ToString("u"));
        Logger.Information($"Finished making a backup of app related data, will try again in {backupIntervalInDays} days");
    }

    public async Task BackupFiles()
    {
        var backupDir = $"{PathHelper.StremDataDirectory}/backups";
        if (!Directory.Exists(backupDir))
        { Directory.CreateDirectory(backupDir); }
        
        var dateFormat = DateTime.Now.ToString("yyMMdd");
        var backupFile = $"{backupDir}/data-backup-{dateFormat}.zip";
        if (File.Exists(backupFile)) { return; }
        
        Database.Checkpoint();
        
        using var zip = ZipFile.Open(backupFile, ZipArchiveMode.Create);
        { zip.CreateEntryFromGlob(PathHelper.StremDataDirectory, "*.db"); }
    }

    public void SetupRegistries()
    {
        var integrationDescriptors = Services.GetServices<IIntegrationDescriptor>();
        IntegrationRegistry?.AddMany(integrationDescriptors);
        
        var menuDescriptors = Services.GetServices<MenuDescriptor>();
        MenuRegistry?.AddMany(menuDescriptors);
    }
    
    public void Dispose()
    { _subs?.Dispose(); }
}