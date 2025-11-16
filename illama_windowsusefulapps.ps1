# ============================================================================
# GESTIONNAIRE SYSTÈME WINDOWS UNIFIÉ
# Combine: Task Scheduler, Language Manager, Keyboard Remapper, Service Manager
# ============================================================================
# IMPORTANT: Nécessite les droits administrateur
# ============================================================================

# Vérification des privilèges administrateur
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "Relancement en tant qu'administrateur..." -ForegroundColor Yellow
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Start-Process powershell.exe -ArgumentList $arguments -Verb RunAs
    exit
}

# ============================================================================
# VARIABLES GLOBALES
# ============================================================================

$global:KeyNames = @{
    'A' = 0x1E; 'B' = 0x30; 'C' = 0x2E; 'D' = 0x20; 'E' = 0x12
    'F' = 0x21; 'G' = 0x22; 'H' = 0x23; 'I' = 0x17; 'J' = 0x24
    'K' = 0x25; 'L' = 0x26; 'M' = 0x32; 'N' = 0x31; 'O' = 0x18
    'P' = 0x19; 'Q' = 0x10; 'R' = 0x13; 'S' = 0x1F; 'T' = 0x14
    'U' = 0x16; 'V' = 0x2F; 'W' = 0x11; 'X' = 0x2D; 'Y' = 0x15; 'Z' = 0x2C
    '1' = 0x02; '2' = 0x03; '3' = 0x04; '4' = 0x05; '5' = 0x06
    '6' = 0x07; '7' = 0x08; '8' = 0x09; '9' = 0x0A; '0' = 0x0B
    'ESC' = 0x01; 'ESCAPE' = 0x01; 'TAB' = 0x0F
    'CAPSLOCK' = 0x3A; 'CAPS' = 0x3A
    'SHIFT' = 0x2A; 'LSHIFT' = 0x2A; 'LEFTSHIFT' = 0x2A
    'RSHIFT' = 0x36; 'RIGHTSHIFT' = 0x36
    'CTRL' = 0x1D; 'LCTRL' = 0x1D; 'LEFTCTRL' = 0x1D; 'CONTROL' = 0x1D
    'ALT' = 0x38; 'LALT' = 0x38; 'LEFTALT' = 0x38
    'SPACE' = 0x39; 'SPACEBAR' = 0x39
    'ENTER' = 0x1C; 'RETURN' = 0x1C; 'BACKSPACE' = 0x0E
    'DELETE' = 0xE053; 'DEL' = 0xE053
    'INSERT' = 0xE052; 'INS' = 0xE052
    'F1' = 0x3B; 'F2' = 0x3C; 'F3' = 0x3D; 'F4' = 0x3E; 'F5' = 0x3F
    'F6' = 0x40; 'F7' = 0x41; 'F8' = 0x42; 'F9' = 0x43; 'F10' = 0x44
    'F11' = 0x57; 'F12' = 0x58
    'MINUS' = 0x0C; 'EQUALS' = 0x0D
    'LEFTBRACKET' = 0x1A; 'RIGHTBRACKET' = 0x1B
    'SEMICOLON' = 0x27; 'QUOTE' = 0x28; 'BACKSLASH' = 0x2B
    'COMMA' = 0x33; 'PERIOD' = 0x34; 'DOT' = 0x34; 'POINT' = 0x34
    'SLASH' = 0x35; 'GRAVE' = 0x29
    'UP' = 0xE048; 'UPARROW' = 0xE048
    'DOWN' = 0xE050; 'DOWNARROW' = 0xE050
    'LEFT' = 0xE04B; 'LEFTARROW' = 0xE04B
    'RIGHT' = 0xE04D; 'RIGHTARROW' = 0xE04D
    'PAGEUP' = 0xE049; 'PGUP' = 0xE049
    'PAGEDOWN' = 0xE051; 'PGDN' = 0xE051
    'HOME' = 0xE047; 'END' = 0xE04F
    'NUMLOCK' = 0x45
    'NUM0' = 0x52; 'NUM1' = 0x4F; 'NUM2' = 0x50; 'NUM3' = 0x51
    'NUM4' = 0x4B; 'NUM5' = 0x4C; 'NUM6' = 0x4D
    'NUM7' = 0x47; 'NUM8' = 0x48; 'NUM9' = 0x49
    'PRINTSCREEN' = 0x54; 'PRTSC' = 0x54
    'SCROLLLOCK' = 0x46; 'PAUSE' = 0x45
    'WIN' = 0xE05B; 'WINDOWS' = 0xE05B; 'LWIN' = 0xE05B
    'RWIN' = 0xE05C; 'MENU' = 0xE05D; 'APP' = 0xE05D
    'DISABLE' = 0x00; 'DISABLED' = 0x00; 'NONE' = 0x00; 'OFF' = 0x00
}

# ============================================================================
# MENU PRINCIPAL
# ============================================================================

function Show-MainMenu {
    Clear-Host
    Write-Host "========================================================================" -ForegroundColor Cyan
    Write-Host "                GESTIONNAIRE SYSTÈME WINDOWS UNIFIÉ                     " -ForegroundColor Cyan
    Write-Host "========================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Gestionnaire des Applications au Démarrage" -ForegroundColor Green
    Write-Host "2. Gestionnaire de Langues" -ForegroundColor Yellow
    Write-Host "3. Remapping de Clavier" -ForegroundColor Magenta
    Write-Host "4. Gestionnaire de Services Windows" -ForegroundColor Cyan
    Write-Host "5. Quitter" -ForegroundColor Red
    Write-Host ""
    Write-Host "========================================================================" -ForegroundColor Cyan
    Write-Host ""
}

# ============================================================================
# MODULE 1: TASK SCHEDULER
# ============================================================================

function Show-TaskSchedulerMenu {
    Clear-Host
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host "   GESTIONNAIRE DES APPLICATIONS AU DÉMARRAGE          " -ForegroundColor Cyan
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Afficher toutes les applications au démarrage" -ForegroundColor Green
    Write-Host "2. Activer une application" -ForegroundColor Green
    Write-Host "3. Désactiver une application" -ForegroundColor Yellow
    Write-Host "4. Rechercher une application" -ForegroundColor Cyan
    Write-Host "5. Afficher les statistiques" -ForegroundColor Magenta
    Write-Host "6. Retour au menu principal" -ForegroundColor Red
    Write-Host ""
}

function Get-StartupApps {
    Write-Host "Récupération des applications au démarrage..." -ForegroundColor Cyan
    
    $apps = @()
    
    $regPaths = @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    )
    
    foreach ($path in $regPaths) {
        if (Test-Path $path) {
            $items = Get-ItemProperty -Path $path
            $items.PSObject.Properties | Where-Object { $_.Name -notlike "PS*" } | ForEach-Object {
                $apps += [PSCustomObject]@{
                    Nom = $_.Name
                    Chemin = $_.Value
                    Type = "Registre"
                    Emplacement = $path
                    Statut = "Active"
                }
            }
        }
    }
    
    $tasks = Get-ScheduledTask | Where-Object { 
        $_.Settings.Enabled -and 
        $_.Triggers.CimClass.CimClassName -like "*LogonTrigger*"
    }
    
    foreach ($task in $tasks) {
        $apps += [PSCustomObject]@{
            Nom = $task.TaskName
            Chemin = $task.Actions[0].Execute
            Type = "Tâche planifiée"
            Emplacement = $task.TaskPath
            Statut = if ($task.State -eq "Ready") { "Active" } else { "Désactivé" }
        }
    }
    
    $startupFolders = @(
        "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup",
        "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup"
    )
    
    foreach ($folder in $startupFolders) {
        if (Test-Path $folder) {
            Get-ChildItem -Path $folder | ForEach-Object {
                $apps += [PSCustomObject]@{
                    Nom = $_.Name
                    Chemin = $_.FullName
                    Type = "Dossier Démarrage"
                    Emplacement = $folder
                    Statut = "Active"
                }
            }
        }
    }
    
    return $apps
}

function Show-AllApps {
    $apps = Get-StartupApps
    
    if ($apps.Count -eq 0) {
        Write-Host "Aucune application au démarrage trouvée." -ForegroundColor Yellow
    } else {
        Write-Host "`nApplications au démarrage trouvées : $($apps.Count)" -ForegroundColor Green
        Write-Host "===============================================================" -ForegroundColor Gray
        
        $index = 1
        foreach ($app in $apps) {
            $statusColor = if ($app.Statut -eq "Active") { "Green" } else { "Red" }
            $statusIcon = if ($app.Statut -eq "Active") { "[OK]" } else { "[X]" }
            
            Write-Host "`n[$index] " -NoNewline -ForegroundColor Cyan
            Write-Host "$($app.Nom)" -ForegroundColor White
            Write-Host "    Type      : $($app.Type)" -ForegroundColor Gray
            Write-Host "    Statut    : " -NoNewline -ForegroundColor Gray
            Write-Host "$statusIcon $($app.Statut)" -ForegroundColor $statusColor
            Write-Host "    Chemin    : $($app.Chemin)" -ForegroundColor DarkGray
            
            $index++
        }
    }
    
    Write-Host "`n===============================================================" -ForegroundColor Gray
}

function Disable-StartupApp {
    $apps = Get-StartupApps | Where-Object { $_.Statut -eq "Active" }
    
    if ($apps.Count -eq 0) {
        Write-Host "Aucune application active à désactiver." -ForegroundColor Yellow
        return
    }
    
    Write-Host "`nApplications actives :" -ForegroundColor Green
    $index = 1
    foreach ($app in $apps) {
        Write-Host "[$index] $($app.Nom) - $($app.Type)" -ForegroundColor Cyan
        $index++
    }
    
    $choice = Read-Host "`nNuméro de l'application à désactiver (0 pour annuler)"
    
    if ($choice -eq "0" -or $choice -eq "") { return }
    
    $selectedApp = $apps[$choice - 1]
    
    if ($null -eq $selectedApp) {
        Write-Host "Choix invalide!" -ForegroundColor Red
        return
    }
    
    try {
        if ($selectedApp.Type -eq "Tâche planifiée") {
            Disable-ScheduledTask -TaskName $selectedApp.Nom -TaskPath $selectedApp.Emplacement -ErrorAction Stop
            Write-Host "[OK] Application '$($selectedApp.Nom)' désactivée avec succès!" -ForegroundColor Green
        }
        elseif ($selectedApp.Type -eq "Registre") {
            Remove-ItemProperty -Path $selectedApp.Emplacement -Name $selectedApp.Nom -ErrorAction Stop
            Write-Host "[OK] Application '$($selectedApp.Nom)' désactivée avec succès!" -ForegroundColor Green
        }
        elseif ($selectedApp.Type -eq "Dossier Démarrage") {
            $disabledPath = $selectedApp.Chemin + ".disabled"
            Rename-Item -Path $selectedApp.Chemin -NewName $disabledPath -ErrorAction Stop
            Write-Host "[OK] Application '$($selectedApp.Nom)' désactivée avec succès!" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "[ERREUR] Lors de la désactivation : $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Enable-StartupApp {
    Write-Host "`nFonction d'activation en développement" -ForegroundColor Yellow
    Write-Host "Pour réactiver une tâche planifiée, utilisez :" -ForegroundColor Cyan
    Write-Host "Enable-ScheduledTask -TaskName 'NomDeLaTâche'" -ForegroundColor Gray
}

function Search-StartupApp {
    $searchTerm = Read-Host "`nEntrez le nom de l'application à rechercher"
    
    if ([string]::IsNullOrWhiteSpace($searchTerm)) { return }
    
    $apps = Get-StartupApps | Where-Object { $_.Nom -like "*$searchTerm*" }
    
    if ($apps.Count -eq 0) {
        Write-Host "Aucune application trouvée contenant '$searchTerm'" -ForegroundColor Yellow
    } else {
        Write-Host "`n$($apps.Count) application(s) trouvée(s) :" -ForegroundColor Green
        foreach ($app in $apps) {
            Write-Host "`n  - $($app.Nom)" -ForegroundColor Cyan
            Write-Host "    Type   : $($app.Type)" -ForegroundColor Gray
            Write-Host "    Statut : $($app.Statut)" -ForegroundColor Gray
        }
    }
}

function Show-Statistics {
    $apps = Get-StartupApps
    
    $byType = $apps | Group-Object -Property Type
    $byStatus = $apps | Group-Object -Property Statut
    
    Write-Host "`nSTATISTIQUES" -ForegroundColor Magenta
    Write-Host "===============================================================" -ForegroundColor Gray
    Write-Host "Total d'applications : $($apps.Count)" -ForegroundColor White
    
    Write-Host "`nPar type :" -ForegroundColor Cyan
    foreach ($group in $byType) {
        Write-Host "  - $($group.Name): $($group.Count)" -ForegroundColor Gray
    }
    
    Write-Host "`nPar statut :" -ForegroundColor Cyan
    foreach ($group in $byStatus) {
        $color = if ($group.Name -eq "Active") { "Green" } else { "Red" }
        Write-Host "  - $($group.Name): $($group.Count)" -ForegroundColor $color
    }
    Write-Host "===============================================================" -ForegroundColor Gray
}

# ============================================================================
# MODULE 2: LANGUAGE MANAGER
# ============================================================================

function Start-LanguageManager {
    Clear-Host
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  SUPPRESSION DE TOUTES LES LANGUES" -ForegroundColor Cyan
    Write-Host "  (Sauf celle que vous choisissez)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    Write-Host "Chargement des langues installées..." -ForegroundColor Yellow
    Write-Host ""
    
    try {
        $installedLanguages = Get-WinUserLanguageList
        
        if ($installedLanguages.Count -eq 0) {
            Write-Host "Aucune langue trouvée!" -ForegroundColor Red
            pause
            return
        }
        
        Write-Host "LANGUES ACTUELLEMENT INSTALLÉES:" -ForegroundColor Green
        Write-Host "================================" -ForegroundColor Green
        for ($i = 0; $i -lt $installedLanguages.Count; $i++) {
            $lang = $installedLanguages[$i]
            Write-Host "[$i] $($lang.LanguageTag) - $($lang.DisplayName)" -ForegroundColor White
        }
        
        Write-Host ""
        Write-Host "================================" -ForegroundColor Green
        Write-Host ""
        
        Write-Host "Quelle langue voulez-vous GARDER?" -ForegroundColor Yellow
        Write-Host "(Toutes les autres seront SUPPRIMÉES DÉFINITIVEMENT)" -ForegroundColor Red
        Write-Host ""
        $choice = Read-Host "Entrez le numéro [0-$($installedLanguages.Count - 1)]"
        
        if ($choice -notmatch '^\d+$' -or [int]$choice -lt 0 -or [int]$choice -ge $installedLanguages.Count) {
            Write-Host ""
            Write-Host "ERREUR: Choix invalide!" -ForegroundColor Red
            pause
            return
        }
        
        $languageToKeep = $installedLanguages[[int]$choice]
        
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "LANGUE À GARDER:" -ForegroundColor Green
        Write-Host "  $($languageToKeep.LanguageTag) - $($languageToKeep.DisplayName)" -ForegroundColor White
        Write-Host ""
        Write-Host "LANGUES À SUPPRIMER:" -ForegroundColor Red
        
        $languagesToRemove = $installedLanguages | Where-Object { $_.LanguageTag -ne $languageToKeep.LanguageTag }
        
        foreach ($lang in $languagesToRemove) {
            Write-Host "  - $($lang.LanguageTag) - $($lang.DisplayName)" -ForegroundColor Red
        }
        
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "ATTENTION: Cette action est IRRÉVERSIBLE!" -ForegroundColor Yellow
        $confirm = Read-Host "Tapez 'OUI' en majuscules pour confirmer"
        
        if ($confirm -ne "OUI") {
            Write-Host ""
            Write-Host "Opération annulée." -ForegroundColor Yellow
            pause
            return
        }
        
        Write-Host ""
        Write-Host "Démarrage de la suppression..." -ForegroundColor Green
        Write-Host ""
        
        Write-Host "[1/3] Suppression des langues via l'API Windows..." -ForegroundColor Green
        
        try {
            $newLanguageList = @($languageToKeep)
            Set-WinUserLanguageList $newLanguageList -Force
            Write-Host "  OK Toutes les langues supprimées sauf $($languageToKeep.LanguageTag)" -ForegroundColor Green
        } catch {
            Write-Host "  X Erreur lors de la suppression: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        Write-Host ""
        Write-Host "[2/3] Blocage de l'ajout automatique de langues..." -ForegroundColor Green
        
        $policyPaths = @(
            "HKLM:\SOFTWARE\Policies\Microsoft\Control Panel\International",
            "HKCU:\SOFTWARE\Policies\Microsoft\Control Panel\International"
        )
        
        foreach ($path in $policyPaths) {
            try {
                if (-not (Test-Path $path)) {
                    New-Item -Path $path -Force | Out-Null
                }
                
                Set-ItemProperty -Path $path -Name "BlockUserInputMethodsForSignIn" -Value 1 -Type DWord -Force
                Set-ItemProperty -Path $path -Name "RestrictLanguagePacksAndFeaturesInstall" -Value 1 -Type DWord -Force
                Write-Host "  OK Stratégies appliquées: $path" -ForegroundColor Green
            } catch {
                Write-Host "  X Erreur: $path" -ForegroundColor Red
            }
        }
        
        Write-Host ""
        Write-Host "[3/3] Désactivation synchronisation..." -ForegroundColor Green
        
        try {
            $syncPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync\Groups\Language"
            if (-not (Test-Path $syncPath)) {
                New-Item -Path $syncPath -Force | Out-Null
            }
            Set-ItemProperty -Path $syncPath -Name "Enabled" -Value 0 -Type DWord -Force
            Write-Host "  OK Synchronisation des langues désactivée" -ForegroundColor Green
        } catch {
            Write-Host "  X Erreur sync" -ForegroundColor Red
        }
        
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "TERMINÉ AVEC SUCCÈS!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Langue conservée: $($languageToKeep.LanguageTag) - $($languageToKeep.DisplayName)" -ForegroundColor Green
        Write-Host "Langues supprimées: $($languagesToRemove.Count)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "ACTIONS REQUISES:" -ForegroundColor Yellow
        Write-Host "1. REDÉMARREZ votre ordinateur MAINTENANT" -ForegroundColor White
        Write-Host ""
        
    } catch {
        Write-Host ""
        Write-Host "ERREUR FATALE: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
    }
    
    pause
}

# ============================================================================
# MODULE 3: KEYBOARD REMAPPER
# ============================================================================

function Get-Scancode {
    param([string]$KeyName)
    $KeyName = $KeyName.ToUpper().Trim()
    if ($global:KeyNames.ContainsKey($KeyName)) {
        return $global:KeyNames[$KeyName]
    }
    if ($KeyName -match '^(0x)?([0-9A-F]+)$') {
        try {
            return [Convert]::ToInt32($matches[2], 16)
        } catch {
            return $null
        }
    }
    return $null
}

function Get-KeyName {
    param([int]$Scancode)
    foreach ($key in $global:KeyNames.Keys) {
        if ($global:KeyNames[$key] -eq $Scancode) {
            return $key
        }
    }
    return "0x$($Scancode.ToString('X'))"
}

function Show-AvailableKeys {
    Write-Host ""
    Write-Host "===================================================================" -ForegroundColor Cyan
    Write-Host "            TOUCHES DISPONIBLES POUR LE REMAPPING" -ForegroundColor Cyan
    Write-Host "===================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Lettres : A B C D E F G H I J K L M N O P Q R S T U V W X Y Z" -ForegroundColor Gray
    Write-Host "Chiffres : 0 1 2 3 4 5 6 7 8 9" -ForegroundColor Gray
    Write-Host "Fonction : F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12" -ForegroundColor Gray
    Write-Host "Modificateurs : CTRL ALT SHIFT CAPSLOCK WIN" -ForegroundColor Gray
    Write-Host "Navigation : UP DOWN LEFT RIGHT HOME END PAGEUP PAGEDOWN" -ForegroundColor Gray
    Write-Host "Édition : ENTER BACKSPACE DELETE INSERT TAB ESC SPACE" -ForegroundColor Gray
    Write-Host "Ponctuation : PERIOD COMMA SEMICOLON QUOTE SLASH MINUS EQUALS" -ForegroundColor Gray
    Write-Host "Spécial : DISABLE (pour désactiver une touche)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "===================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Set-KeyboardRemap {
    param([hashtable]$Mappings)
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Keyboard Layout"
    if (-not (Test-Path $regPath)) {
        New-Item -Path $regPath -Force | Out-Null
    }
    $numMappings = $Mappings.Count
    [byte[]]$scancodeMap = @(0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00)
    $entryCount = $numMappings + 1
    $scancodeMap += [BitConverter]::GetBytes([uint32]$entryCount)
    foreach ($mapping in $Mappings.GetEnumerator()) {
        $source = $mapping.Key
        $destination = $mapping.Value
        $scancodeMap += [byte]($destination -band 0xFF)
        $scancodeMap += [byte](($destination -shr 8) -band 0xFF)
        $scancodeMap += [byte]($source -band 0xFF)
        $scancodeMap += [byte](($source -shr 8) -band 0xFF)
    }
    $scancodeMap += @(0x00, 0x00, 0x00, 0x00)
    try {
        Set-ItemProperty -Path $regPath -Name "Scancode Map" -Value $scancodeMap -Type Binary -Force
        return $true
    } catch {
        Write-Error "Erreur lors de la création du remapping: $_"
        return $false
    }
}

function Remove-KeyboardRemap {
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Keyboard Layout"
    try {
        Remove-ItemProperty -Path $regPath -Name "Scancode Map" -ErrorAction Stop
        return $true
    } catch {
        return $false
    }
}

function Get-CurrentRemap {
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Keyboard Layout"
    
    try {
        $scancodeMap = Get-ItemProperty -Path $regPath -Name "Scancode Map" -ErrorAction Stop
        $bytes = $scancodeMap.'Scancode Map'
        
        Write-Host ""
        Write-Host "===================================================================" -ForegroundColor Cyan
        Write-Host "            REMAPPING ACTUELLEMENT CONFIGURÉ" -ForegroundColor Cyan
        Write-Host "===================================================================" -ForegroundColor Cyan
        Write-Host ""
        
        $numEntries = [BitConverter]::ToUInt32($bytes, 8)
        
        if ($numEntries -le 1) {
            Write-Host "Aucun remapping actif." -ForegroundColor Yellow
            Write-Host ""
            return $false
        }
        
        Write-Host "Mappings actifs:" -ForegroundColor Green
        Write-Host ""
        for ($i = 0; $i -lt ($numEntries - 1); $i++) {
            $offset = 12 + ($i * 4)
            $dest = [BitConverter]::ToUInt16($bytes, $offset)
            $source = [BitConverter]::ToUInt16($bytes, $offset + 2)
            
            $sourceName = Get-KeyName $source
            $destName = Get-KeyName $dest
            
            if ($dest -eq 0) {
                Write-Host "  $sourceName -> DÉSACTIVÉ" -ForegroundColor White
            } else {
                Write-Host "  $sourceName -> $destName" -ForegroundColor White
            }
        }
        
        Write-Host ""
        Write-Host "===================================================================" -ForegroundColor Cyan
        Write-Host ""
        
        return $true
        
    } catch {
        Write-Host ""
        Write-Host "Aucun remapping configuré actuellement." -ForegroundColor Yellow
        Write-Host ""
        return $false
    }
}

function Show-KeyboardRemapperMenu {
    Clear-Host
    Write-Host "===================================================================" -ForegroundColor Cyan
    Write-Host "       REMAPPING DE CLAVIER INTERACTIF - SCANCODE MAP            " -ForegroundColor Cyan
    Write-Host "===================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Créer un nouveau remapping" -ForegroundColor White
    Write-Host "2. Voir les touches disponibles" -ForegroundColor White
    Write-Host "3. Voir le remapping actuel" -ForegroundColor White
    Write-Host "4. Supprimer le remapping actuel" -ForegroundColor White
    Write-Host "5. Retour au menu principal" -ForegroundColor White
    Write-Host "===================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Start-KeyboardRemapper {
    $running = $true
    while ($running) {
        Show-KeyboardRemapperMenu
        $choice = Read-Host "Votre choix (1-5)"
        
        if ($choice -eq "1") {
            Clear-Host
            Write-Host ""
            Write-Host "CONFIGURATION DU REMAPPING" -ForegroundColor Cyan
            Write-Host "===========================" -ForegroundColor Cyan
            Write-Host ""
            
            $mappings = @{}
            $continue = $true
            
            while ($continue) {
                Write-Host "-------------------------------------------------------------------" -ForegroundColor Gray
                
                $sourceCode = $null
                while ($null -eq $sourceCode) {
                    Write-Host ""
                    Write-Host "Quelle touche voulez-vous remapper ?" -ForegroundColor Yellow
                    Write-Host "(ex: CAPSLOCK, ESC, A, F1, 0x3A...)" -ForegroundColor Gray
                    $sourceKey = Read-Host "Touche source"
                    
                    if ([string]::IsNullOrWhiteSpace($sourceKey)) {
                        Write-Host "Veuillez entrer une touche valide" -ForegroundColor Red
                        continue
                    }
                    
                    $sourceCode = Get-Scancode $sourceKey
                    
                    if ($null -eq $sourceCode) {
                        Write-Host "Touche non reconnue" -ForegroundColor Red
                        $showList = Read-Host "Voir la liste des touches? (o/n)"
                        if ($showList -eq "o") {
                            Show-AvailableKeys
                        }
                    }
                }
                
                $destCode = $null
                while ($null -eq $destCode) {
                    Write-Host ""
                    Write-Host "Vers quelle touche voulez-vous la remapper ?" -ForegroundColor Yellow
                    Write-Host "(ex: PERIOD, CTRL, A, DISABLE...)" -ForegroundColor Gray
                    $destKey = Read-Host "Touche destination"
                    
                    if ([string]::IsNullOrWhiteSpace($destKey)) {
                        Write-Host "Veuillez entrer une touche valide" -ForegroundColor Red
                        continue
                    }
                    
                    $destCode = Get-Scancode $destKey
                    
                    if ($null -eq $destCode) {
                        Write-Host "Touche non reconnue" -ForegroundColor Red
                        $showList = Read-Host "Voir la liste des touches? (o/n)"
                        if ($showList -eq "o") {
                            Show-AvailableKeys
                        }
                    }
                }
                
                $mappings[$sourceCode] = $destCode
                
                $sourceKeyName = Get-KeyName $sourceCode
                $destKeyName = Get-KeyName $destCode
                
                Write-Host ""
                Write-Host "Mapping ajouté: $sourceKeyName -> $destKeyName" -ForegroundColor Green
                
                if ($mappings.Count -gt 0) {
                    Write-Host ""
                    Write-Host "MAPPINGS CONFIGURÉS :" -ForegroundColor Cyan
                    foreach ($m in $mappings.GetEnumerator()) {
                        $sn = Get-KeyName $m.Key
                        $dn = Get-KeyName $m.Value
                        Write-Host "  $sn -> $dn" -ForegroundColor White
                    }
                }
                
                Write-Host ""
                $addMore = Read-Host "Voulez-vous ajouter un autre mapping ? (o/n)"
                if ($addMore -ne "o") {
                    $continue = $false
                }
            }
            
            if ($mappings.Count -gt 0) {
                Write-Host ""
                Write-Host "-------------------------------------------------------------------" -ForegroundColor Gray
                Write-Host "Application du remapping..." -ForegroundColor Yellow
                
                if (Set-KeyboardRemap -Mappings $mappings) {
                    Write-Host ""
                    Write-Host "Remapping créé avec succès!" -ForegroundColor Green
                    Write-Host ""
                    Write-Host "IMPORTANT: Redémarrez votre ordinateur pour appliquer!" -ForegroundColor Yellow
                    Write-Host ""
                } else {
                    Write-Host ""
                    Write-Host "Erreur lors de la création du remapping" -ForegroundColor Red
                    Write-Host ""
                }
            }
            
            pause
        }
        elseif ($choice -eq "2") {
            Show-AvailableKeys
            pause
        }
        elseif ($choice -eq "3") {
            Get-CurrentRemap
            pause
        }
        elseif ($choice -eq "4") {
            Write-Host ""
            Write-Host "Êtes-vous sûr de vouloir supprimer le remapping actuel ? (o/n)" -ForegroundColor Yellow
            $confirm = Read-Host
            
            if ($confirm -eq "o") {
                if (Remove-KeyboardRemap) {
                    Write-Host ""
                    Write-Host "Remapping supprimé avec succès!" -ForegroundColor Green
                    Write-Host "Redémarrez votre ordinateur pour restaurer le comportement normal." -ForegroundColor Yellow
                    Write-Host ""
                } else {
                    Write-Host ""
                    Write-Host "Aucun remapping trouvé ou erreur lors de la suppression." -ForegroundColor Yellow
                    Write-Host ""
                }
            }
            
            pause
        }
        elseif ($choice -eq "5") {
            $running = $false
        }
        else {
            Write-Host ""
            Write-Host "Choix invalide. Veuillez choisir entre 1 et 5." -ForegroundColor Red
            Start-Sleep -Seconds 2
        }
    }
}

# ============================================================================
# MODULE 4: SERVICE MANAGER
# ============================================================================

function Show-ServiceManagerMenu {
    Clear-Host
    Write-Host "========================================================================" -ForegroundColor Cyan
    Write-Host "              GESTIONNAIRE DE SERVICES WINDOWS                          " -ForegroundColor Cyan
    Write-Host "========================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Lister tous les services" -ForegroundColor Green
    Write-Host "2. Lister uniquement les services actifs" -ForegroundColor Green
    Write-Host "3. Lister uniquement les services arrêtés" -ForegroundColor Yellow
    Write-Host "4. Rechercher un service" -ForegroundColor Cyan
    Write-Host "5. Modifier un service (Démarrer/Arrêter/Type)" -ForegroundColor Magenta
    Write-Host "6. Services consommant le plus de ressources" -ForegroundColor Red
    Write-Host "7. Services recommandés à désactiver" -ForegroundColor Yellow
    Write-Host "8. Retour au menu principal" -ForegroundColor Red
    Write-Host ""
    Write-Host "========================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Get-AllServices {
    param(
        [string]$Filter = "All"
    )
    
    Write-Host "Recuperation des services..." -ForegroundColor Cyan
    Write-Host "Veuillez patienter..." -ForegroundColor Yellow
    
    # Récupérer tous les services via WMI en une seule requête (plus rapide)
    $wmiServices = Get-WmiObject -Class Win32_Service | Group-Object -Property Name -AsHashTable
    
    $services = Get-Service | Sort-Object DisplayName
    
    if ($Filter -eq "Running") {
        $services = $services | Where-Object { $_.Status -eq "Running" }
    }
    elseif ($Filter -eq "Stopped") {
        $services = $services | Where-Object { $_.Status -eq "Stopped" }
    }
    
    $serviceDetails = @()
    $count = 0
    $total = $services.Count
    
    foreach ($service in $services) {
        $count++
        if ($count % 50 -eq 0) {
            Write-Host "Progression: $count/$total services traites..." -ForegroundColor Gray
        }
        
        $wmiService = $wmiServices[$service.Name]
        
        $startupType = "Inconnu"
        if ($wmiService) {
            $startupType = switch ($wmiService.StartMode) {
                "Auto" { "Automatique" }
                "Manual" { "Manuel" }
                "Disabled" { "Desactive" }
                default { $wmiService.StartMode }
            }
        }
        
        $serviceDetails += [PSCustomObject]@{
            Nom = $service.Name
            NomAffichage = $service.DisplayName
            Statut = $service.Status
            TypeDemarrage = $startupType
            Description = if ($wmiService) { $wmiService.Description } else { "N/A" }
        }
    }
    
    Write-Host "Termine! $total services recuperes." -ForegroundColor Green
    
    return $serviceDetails
}

function Show-ServicesList {
    param(
        [string]$Filter = "All"
    )
    
    $services = Get-AllServices -Filter $Filter
    
    if ($services.Count -eq 0) {
        Write-Host "`nAucun service trouve." -ForegroundColor Yellow
        return
    }
    
    $title = "TOUS LES SERVICES"
    if ($Filter -eq "Running") { $title = "SERVICES ACTIFS" }
    elseif ($Filter -eq "Stopped") { $title = "SERVICES ARRETES" }
    
    Write-Host "`n$title : $($services.Count)" -ForegroundColor Green
    Write-Host "========================================================================" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Affichage simplifie pour ameliorer les performances..." -ForegroundColor Yellow
    Write-Host "Utilisez l'option 'Rechercher' pour plus de details sur un service." -ForegroundColor Yellow
    Write-Host ""
    
    $index = 1
    foreach ($service in $services) {
        $statusColor = if ($service.Statut -eq "Running") { "Green" } else { "Red" }
        $statusIcon = if ($service.Statut -eq "Running") { "●" } else { "○" }
        
        # Affichage compact sur une ligne
        Write-Host "[$index] " -NoNewline -ForegroundColor Cyan
        Write-Host "$statusIcon " -NoNewline -ForegroundColor $statusColor
        Write-Host "$($service.NomAffichage) " -NoNewline -ForegroundColor White
        Write-Host "($($service.Nom)) - " -NoNewline -ForegroundColor Gray
        Write-Host "$($service.TypeDemarrage)" -ForegroundColor DarkGray
        
        $index++
        
        # Pause tous les 30 services pour éviter le défilement trop rapide
        if ($index % 30 -eq 0) {
            Write-Host ""
            Write-Host "--- Appuyez sur une touche pour continuer ---" -ForegroundColor Yellow
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            Write-Host ""
        }
    }
    
    Write-Host "`n========================================================================" -ForegroundColor Gray
}

function Search-Service {
    $searchTerm = Read-Host "`nEntrez le nom du service à rechercher"
    
    if ([string]::IsNullOrWhiteSpace($searchTerm)) { return }
    
    $services = Get-AllServices | Where-Object { 
        $_.Nom -like "*$searchTerm*" -or $_.NomAffichage -like "*$searchTerm*" 
    }
    
    if ($services.Count -eq 0) {
        Write-Host "`nAucun service trouvé contenant '$searchTerm'" -ForegroundColor Yellow
    } else {
        Write-Host "`n$($services.Count) service(s) trouvé(s) :" -ForegroundColor Green
        Write-Host "========================================================================" -ForegroundColor Gray
        
        foreach ($service in $services) {
            $statusColor = if ($service.Statut -eq "Running") { "Green" } else { "Red" }
            
            Write-Host "`n● $($service.NomAffichage)" -ForegroundColor Cyan
            Write-Host "  Nom           : $($service.Nom)" -ForegroundColor Gray
            Write-Host "  Statut        : $($service.Statut)" -ForegroundColor $statusColor
            Write-Host "  Type démarrage: $($service.TypeDemarrage)" -ForegroundColor Gray
        }
        
        Write-Host "`n========================================================================" -ForegroundColor Gray
    }
}

function Modify-Service {
    Write-Host "`nMODIFICATION D'UN SERVICE" -ForegroundColor Cyan
    Write-Host "========================================================================" -ForegroundColor Gray
    
    $serviceName = Read-Host "`nEntrez le nom exact du service (ex: wuauserv)"
    
    if ([string]::IsNullOrWhiteSpace($serviceName)) { return }
    
    try {
        $service = Get-Service -Name $serviceName -ErrorAction Stop
        $wmiService = Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'" -ErrorAction Stop
        
        Write-Host "`nService trouve: $($service.DisplayName)" -ForegroundColor Green
        Write-Host "Statut actuel: $($service.Status)" -ForegroundColor Gray
        Write-Host "Type de demarrage: $($wmiService.StartMode)" -ForegroundColor Gray
        
        Write-Host "`n========================================================================" -ForegroundColor Gray
        Write-Host "QUE VOULEZ-VOUS FAIRE ?" -ForegroundColor Yellow
        Write-Host "========================================================================" -ForegroundColor Gray
        Write-Host "1. Demarrer le service" -ForegroundColor Green
        Write-Host "2. Arreter le service" -ForegroundColor Red
        Write-Host "3. Redemarrer le service" -ForegroundColor Yellow
        Write-Host "4. Changer le type de demarrage" -ForegroundColor Cyan
        Write-Host "5. Annuler" -ForegroundColor Gray
        Write-Host ""
        
        $action = Read-Host "Votre choix (1-5)"
        
        if ($action -eq "1") {
            if ($service.Status -eq "Running") {
                Write-Host "`nLe service est deja en cours d'execution." -ForegroundColor Yellow
            } else {
                try {
                    Write-Host "`nDemarrage du service..." -ForegroundColor Cyan
                    Start-Service -Name $serviceName -ErrorAction Stop
                    Write-Host "Service demarre avec succes!" -ForegroundColor Green
                } catch {
                    Write-Host "Erreur lors du demarrage: $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        }
        elseif ($action -eq "2") {
            if ($service.Status -eq "Stopped") {
                Write-Host "`nLe service est deja arrete." -ForegroundColor Yellow
            } else {
                try {
                    Write-Host "`nArret du service..." -ForegroundColor Cyan
                    Stop-Service -Name $serviceName -Force -ErrorAction Stop
                    Write-Host "Service arrete avec succes!" -ForegroundColor Green
                } catch {
                    Write-Host "Erreur lors de l'arret: $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        }
        elseif ($action -eq "3") {
            try {
                Write-Host "`nRedemarrage du service..." -ForegroundColor Cyan
                Restart-Service -Name $serviceName -Force -ErrorAction Stop
                Write-Host "Service redemarre avec succes!" -ForegroundColor Green
            } catch {
                Write-Host "Erreur lors du redemarrage: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        elseif ($action -eq "4") {
            Write-Host "`nTYPE DE DEMARRAGE :" -ForegroundColor Yellow
            Write-Host "1. Automatique" -ForegroundColor White
            Write-Host "2. Manuel" -ForegroundColor White
            Write-Host "3. Desactive" -ForegroundColor White
            Write-Host ""
            
            $startupChoice = Read-Host "Votre choix (1-3)"
            
            $startupType = $null
            if ($startupChoice -eq "1") { $startupType = "Automatic" }
            elseif ($startupChoice -eq "2") { $startupType = "Manual" }
            elseif ($startupChoice -eq "3") { $startupType = "Disabled" }
            
            if ($startupType) {
                try {
                    Write-Host "`nModification du type de demarrage..." -ForegroundColor Cyan
                    Set-Service -Name $serviceName -StartupType $startupType -ErrorAction Stop
                    Write-Host "Type de demarrage modifie avec succes!" -ForegroundColor Green
                } catch {
                    Write-Host "Erreur lors de la modification: $($_.Exception.Message)" -ForegroundColor Red
                }
            } else {
                Write-Host "`nChoix invalide." -ForegroundColor Red
            }
        }
        elseif ($action -eq "5") {
            Write-Host "`nOperation annulee." -ForegroundColor Yellow
        }
        else {
            Write-Host "`nChoix invalide." -ForegroundColor Red
        }
        
    } catch {
        Write-Host "`nERREUR: Service non trouve ou acces refuse." -ForegroundColor Red
        Write-Host "Details: $($_.Exception.Message)" -ForegroundColor Gray
    }
}

function Show-ResourceIntensiveServices {
    Write-Host "`nAnalyse des services consommant le plus de ressources..." -ForegroundColor Cyan
    Write-Host "(Cela peut prendre quelques secondes)" -ForegroundColor Gray
    Write-Host ""
    
    try {
        $processes = Get-Process | Where-Object { $_.SessionId -eq 0 } | Sort-Object -Property CPU -Descending | Select-Object -First 20
        
        $serviceProcesses = @()
        
        foreach ($proc in $processes) {
            try {
                $service = Get-WmiObject -Class Win32_Service -Filter "ProcessId='$($proc.Id)'" -ErrorAction SilentlyContinue
                
                if ($service) {
                    $serviceProcesses += [PSCustomObject]@{
                        Nom = $service.Name
                        NomAffichage = $service.DisplayName
                        CPU = [math]::Round($proc.CPU, 2)
                        MemoreMB = [math]::Round($proc.WorkingSet64 / 1MB, 2)
                        ProcessId = $proc.Id
                    }
                }
            } catch {
                continue
            }
        }
        
        if ($serviceProcesses.Count -eq 0) {
            Write-Host "Aucun service consommant significativement de ressources trouvé." -ForegroundColor Yellow
            return
        }
        
        Write-Host "TOP SERVICES PAR CONSOMMATION DE RESSOURCES :" -ForegroundColor Green
        Write-Host "========================================================================" -ForegroundColor Gray
        
        $index = 1
        foreach ($svc in $serviceProcesses) {
            Write-Host "`n[$index] $($svc.NomAffichage)" -ForegroundColor Cyan
            Write-Host "    Nom service: $($svc.Nom)" -ForegroundColor Gray
            Write-Host "    CPU (sec)  : $($svc.CPU)" -ForegroundColor Yellow
            Write-Host "    Mémoire    : $($svc.MemoreMB) MB" -ForegroundColor Yellow
            Write-Host "    PID        : $($svc.ProcessId)" -ForegroundColor DarkGray
            
            $index++
        }
        
        Write-Host "`n========================================================================" -ForegroundColor Gray
        
    } catch {
        Write-Host "ERREUR lors de l'analyse: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Show-RecommendedDisableServices {
    Write-Host "`nSERVICES POUVANT ÊTRE DÉSACTIVÉS EN TOUTE SÉCURITÉ" -ForegroundColor Yellow
    Write-Host "(Sur la plupart des systèmes)" -ForegroundColor Gray
    Write-Host "========================================================================" -ForegroundColor Gray
    Write-Host ""
    
    $recommendedDisable = @(
        @{Name="dmwappushservice"; Display="Service de routage de messages push WAP dmwappushservice"; Reason="Télémétrie et collecte de données"},
        @{Name="DiagTrack"; Display="Expériences des utilisateurs connectés et télémétrie"; Reason="Collecte de données d'utilisation"},
        @{Name="RetailDemo"; Display="Service de démonstration du magasin de détail"; Reason="Inutile sauf en magasin"},
        @{Name="XblAuthManager"; Display="Gestionnaire d'authentification Xbox Live"; Reason="Inutile si vous n'utilisez pas Xbox"},
        @{Name="XblGameSave"; Display="Service de sauvegarde de jeux Xbox Live"; Reason="Inutile si vous n'utilisez pas Xbox"},
        @{Name="XboxNetApiSvc"; Display="Service réseau Xbox Live"; Reason="Inutile si vous n'utilisez pas Xbox"},
        @{Name="XboxGipSvc"; Display="Service de gestion des contrôleurs Xbox"; Reason="Inutile sans manette Xbox"},
        @{Name="WSearch"; Display="Recherche Windows"; Reason="Améliore perf. mais désactive la recherche rapide"},
        @{Name="SysMain"; Display="SysMain (Superfetch)"; Reason="Peut ralentir les SSD"},
        @{Name="WbioSrvc"; Display="Service biométrique Windows"; Reason="Inutile sans capteur biométrique"}
    )
    
    $index = 1
    foreach ($rec in $recommendedDisable) {
        $service = Get-Service -Name $rec.Name -ErrorAction SilentlyContinue
        
        if ($service) {
            $statusColor = if ($service.Status -eq "Running") { "Red" } else { "Green" }
            $statusIcon = if ($service.Status -eq "Running") { "●" } else { "○" }
            
            Write-Host "[$index] " -NoNewline -ForegroundColor Cyan
            Write-Host "$($rec.Display)" -ForegroundColor White
            Write-Host "    Nom      : $($rec.Name)" -ForegroundColor Gray
            Write-Host "    Statut   : " -NoNewline -ForegroundColor Gray
            Write-Host "$statusIcon $($service.Status)" -ForegroundColor $statusColor
            Write-Host "    Raison   : $($rec.Reason)" -ForegroundColor Yellow
            Write-Host ""
        }
        
        $index++
    }
    
    Write-Host "========================================================================" -ForegroundColor Gray
    Write-Host "ATTENTION: Désactivez uniquement si vous comprenez les conséquences!" -ForegroundColor Red
    Write-Host "========================================================================" -ForegroundColor Gray
}

function Start-ServiceManager {
    $running = $true
    while ($running) {
        Show-ServiceManagerMenu
        $choice = Read-Host "Votre choix (1-8)"
        
        if ($choice -eq "1") { Show-ServicesList -Filter "All"; pause }
        elseif ($choice -eq "2") { Show-ServicesList -Filter "Running"; pause }
        elseif ($choice -eq "3") { Show-ServicesList -Filter "Stopped"; pause }
        elseif ($choice -eq "4") { Search-Service; pause }
        elseif ($choice -eq "5") { Modify-Service; pause }
        elseif ($choice -eq "6") { Show-ResourceIntensiveServices; pause }
        elseif ($choice -eq "7") { Show-RecommendedDisableServices; pause }
        elseif ($choice -eq "8") { $running = $false }
        else { 
            Write-Host "`nOption invalide!" -ForegroundColor Red
            Start-Sleep -Seconds 2
        }
    }
}

# ============================================================================
# BOUCLE PRINCIPALE
# ============================================================================

Clear-Host
Write-Host ""
Write-Host "Droits administrateur confirmes" -ForegroundColor Green
Write-Host ""
Start-Sleep -Seconds 1

$continueMain = $true
while ($continueMain) {
    Show-MainMenu
    $choice = Read-Host "Choisissez une option (1-5)"
    
    if ($choice -eq "1") {
        $taskRunning = $true
        while ($taskRunning) {
            Show-TaskSchedulerMenu
            $taskChoice = Read-Host "Choisissez une option (1-6)"
            
            if ($taskChoice -eq "1") { Show-AllApps; pause }
            elseif ($taskChoice -eq "2") { Enable-StartupApp; pause }
            elseif ($taskChoice -eq "3") { Disable-StartupApp; pause }
            elseif ($taskChoice -eq "4") { Search-StartupApp; pause }
            elseif ($taskChoice -eq "5") { Show-Statistics; pause }
            elseif ($taskChoice -eq "6") { $taskRunning = $false }
            else { 
                Write-Host "Option invalide!" -ForegroundColor Red
                pause 
            }
        }
    }
    elseif ($choice -eq "2") {
        Start-LanguageManager
    }
    elseif ($choice -eq "3") {
        Start-KeyboardRemapper
    }
    elseif ($choice -eq "4") {
        Start-ServiceManager
    }
    elseif ($choice -eq "5") {
        Write-Host "`nAu revoir!" -ForegroundColor Cyan
        $continueMain = $false
    }
    else {
        Write-Host "Option invalide!" -ForegroundColor Red
        pause
    }
}
