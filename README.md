# 🔧 Gestionnaire Système Windows Unifié

Un outil PowerShell tout-en-un pour gérer efficacement votre système Windows. Combine quatre modules puissants dans une interface interactive et conviviale.

![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B-blue)
![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D6)
![License](https://img.shields.io/badge/license-MIT-green)

## 📋 Table des matières

- [Fonctionnalités](#-fonctionnalités)
- [Prérequis](#-prérequis)
- [Installation](#-installation)
- [Utilisation](#-utilisation)
- [Modules](#-modules)
- [Captures d'écran](#-captures-décran)
- [Avertissements](#-avertissements)
- [FAQ](#-faq)
- [Contribution](#-contribution)
- [Licence](#-licence)

## ✨ Fonctionnalités

### 🚀 **Module 1 : Gestionnaire des Applications au Démarrage**
- Liste toutes les applications qui se lancent au démarrage
- Active/désactive facilement les applications
- Recherche rapide par nom
- Statistiques détaillées
- Gère le registre, les tâches planifiées et les dossiers de démarrage

### 🌍 **Module 2 : Gestionnaire de Langues**
- Supprime toutes les langues Windows sauf celle de votre choix
- Bloque l'ajout automatique de langues
- Désactive la synchronisation des paramètres linguistiques
- Parfait pour nettoyer un système multilingue

### ⌨️ **Module 3 : Remapping de Clavier**
- Remappez n'importe quelle touche vers une autre
- Désactivez des touches indésirables (ex: CapsLock)
- Interface interactive avec liste complète des touches
- Utilise la méthode Scancode Map (niveau système)
- Visualisation du remapping actuel

### 🛠️ **Module 4 : Gestionnaire de Services Windows**
- Liste tous les services (actifs/arrêtés)
- Démarre/arrête/redémarre les services
- Modifie le type de démarrage (Auto/Manuel/Désactivé)
- Affiche les services consommant le plus de ressources
- Suggestions de services à désactiver en toute sécurité

## 📦 Prérequis

- **OS** : Windows 10 ou Windows 11
- **PowerShell** : Version 5.1 ou supérieure
- **Droits** : Administrateur (le script se relance automatiquement avec les bons droits)

## 🔽 Installation

### Méthode 1 : Téléchargement direct

```powershell
# Téléchargez le fichier illama_windowsusefulapps.ps1
# Clic droit > "Exécuter avec PowerShell"
```

### Méthode 2 : Ligne de commande

```powershell
# Ouvrez PowerShell en tant qu'administrateur
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force
.\illama_windowsusefulapps.ps1
```

### Méthode 3 : Clone depuis GitHub

```bash
git clone https://github.com/votre-username/gestionnaire-systeme-windows.git
cd gestionnaire-systeme-windows
powershell -ExecutionPolicy Bypass -File illama_windowsusefulapps.ps1
```

## 🎯 Utilisation

### Lancement

1. **Clic droit** sur le fichier `.ps1` → **Exécuter avec PowerShell**
2. Le script demandera automatiquement les droits administrateur si nécessaire
3. Le menu principal s'affiche

### Navigation

```
========================================================================
                GESTIONNAIRE SYSTÈME WINDOWS UNIFIÉ                     
========================================================================

1. Gestionnaire des Applications au Démarrage
2. Gestionnaire de Langues
3. Remapping de Clavier
4. Gestionnaire de Services Windows
5. Quitter
```

Entrez simplement le numéro de votre choix et suivez les instructions à l'écran.

## 📚 Modules

### 1️⃣ Applications au Démarrage

**Exemple d'utilisation :**
```
1. Afficher toutes les applications
   → Voir la liste complète avec statut et type

3. Désactiver une application
   → Choisir dans la liste
   → Confirmation automatique
```

**Ce qui est géré :**
- ✅ Registre (HKLM et HKCU)
- ✅ Tâches planifiées
- ✅ Dossiers de démarrage

### 2️⃣ Gestionnaire de Langues

**Processus :**
```
1. Affiche toutes les langues installées
2. Vous choisissez celle à GARDER
3. Supprime TOUTES les autres
4. Bloque l'ajout automatique
5. Redémarrage nécessaire
```

⚠️ **ATTENTION** : Cette action est irréversible !

### 3️⃣ Remapping de Clavier

**Exemples de remapping :**

```powershell
# Transformer CapsLock en Ctrl
CAPSLOCK → CTRL

# Désactiver la touche Windows
WIN → DISABLE

# Échanger deux touches
A → B
B → A
```

**Touches supportées :**
- Lettres (A-Z)
- Chiffres (0-9)
- Fonctions (F1-F12)
- Modificateurs (CTRL, ALT, SHIFT, WIN)
- Navigation (flèches, HOME, END, etc.)
- Et bien plus...

### 4️⃣ Services Windows

**Fonctionnalités :**
```
1. Liste complète des services
2. Filtres (actifs/arrêtés)
3. Recherche par nom
4. Modification (démarrer/arrêter/type)
5. Analyse des ressources
6. Recommandations de désactivation
```

**Services souvent désactivables :**
- dmwappushservice (Télémétrie)
- DiagTrack (Collecte de données)
- Services Xbox (si non utilisés)
- Windows Search (ralentit les SSD)

## 📸 Captures d'écran

```
========================================================================
            REMAPPING ACTUELLEMENT CONFIGURÉ
========================================================================

Mappings actifs:

  CAPSLOCK -> CTRL
  WIN -> DÉSACTIVÉ
  ESC -> GRAVE

========================================================================
```

## ⚠️ Avertissements

### 🔴 **Important**

- **Droits administrateur requis** : Toutes les opérations modifient le système
- **Redémarrage nécessaire** : Pour le remapping clavier et le gestionnaire de langues
- **Backup recommandé** : Créez un point de restauration avant modifications importantes
- **Services critiques** : Ne désactivez pas les services sans comprendre leur fonction

### 🟡 **Cas d'utilisation déconseillés**

- Ne désactivez pas `wuauserv` (Windows Update) sur une machine exposée à Internet
- Ne supprimez pas toutes les langues si vous travaillez dans un environnement multilingue
- Ne remappez pas des touches sans avoir un clavier physique de secours

## ❓ FAQ

### Le script ne se lance pas ?

```powershell
# Débloquez le fichier
Unblock-File -Path .\illama_windowsusefulapps.ps1

# Autorisez l'exécution temporaire
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
```

### Comment annuler un remapping de clavier ?

Menu 3 → Option 4 → Supprimer le remapping actuel → Redémarrer

### Les changements ne s'appliquent pas ?

**Redémarrez votre ordinateur** pour :
- Remapping de clavier
- Suppression de langues
- Certaines modifications de services

### Le script est-il sûr ?

Oui, le code est open-source et ne contient :
- ❌ Aucune connexion externe
- ❌ Aucune collecte de données
- ❌ Aucun code malveillant
- ✅ Uniquement des commandes PowerShell standard

## 🤝 Contribution

Les contributions sont les bienvenues !

```bash
# Fork le projet
# Créez une branche
git checkout -b feature/amelioration

# Commit vos changements
git commit -m "Ajout d'une fonctionnalité"

# Push vers la branche
git push origin feature/amelioration

# Ouvrez une Pull Request
```

## 📝 Licence

Ce projet est sous licence MIT. Voir le fichier `LICENSE` pour plus de détails.

---

## 🌟 Remerciements

- Communauté PowerShell
- Testeurs et contributeurs
- Utilisateurs qui remontent des bugs

## 📧 Contact

- **Issues** : [GitHub Issues](https://github.com/votre-username/gestionnaire-systeme-windows/issues)
- **Discussions** : [GitHub Discussions](https://github.com/votre-username/gestionnaire-systeme-windows/discussions)

---
