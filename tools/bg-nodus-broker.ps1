# Configuration de l'encodage
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['*:Encoding'] = 'UTF8'

$originalDirectory = Get-Location

# Détection de la branche Git
try {
    $gitBranch = (git rev-parse --abbrev-ref HEAD).Trim()
	Write-Host "Branche Git détectée : $gitBranch" -ForegroundColor Cyan
} catch {
    $gitBranch = ""
}

if ($args.Count -gt 0) {
    $target = $args[0]
} else {
    $target = "win-x64"
}

# Fonctions
function Test-NuGetConnection {
    $originalProgress = $ProgressPreference
    try {
        Write-Host "Test de connexion à NuGet.org... " -NoNewline
        $ProgressPreference = 'SilentlyContinue'
        # Utiliser Head pour juste vérifier la disponibilité (plus rapide)
        $response = Invoke-WebRequest -Uri "https://api.nuget.org/v3/index.json" -Method Head -TimeoutSec 2 -UseBasicParsing
        return $true
    } catch {
        return $false
    } finally {
        $ProgressPreference = $originalProgress
    }
}

function Invoke-DotNetPublish {
    param(
        [string]$Target,
        [bool]$Online
    )

    Write-Host

    $publishArgs = @(
        "publish",
        "-c", "Release",
        "-r", $Target,
        "-p:PublishSingleFile=true",
		"-p:ContinuousIntegrationBuild=true",
		"-p:GenerateDocumentationFile=false",
        "--self-contained", "true",
        "-o", "./dist/$Target"
    )
    # ➕ Gestion automatique du hash Git
	if ($gitBranch -notlike "feature/*") {
		Write-Host "Version propre (sans hash Git)" -ForegroundColor Green
		$publishArgs += "-p:IncludeSourceRevisionInInformationalVersion=false"
	} else {
		Write-Host "Version feature (hash Git activé)" -ForegroundColor Yellow
	}

    if (-not $Online) {
        $publishArgs += "-p:RestoreSources=`"nupkg`""
    }

    & dotnet @publishArgs 2>&1 | ForEach-Object {
        if ($_ -match 'error' -or $_ -match 'erreur') {
            Write-Host $_ -ForegroundColor Red
        }
        elseif ($_ -match 'warning') {
            Write-Host $_ -ForegroundColor Yellow
        }
    }

    return $LASTEXITCODE

    #Write-Host "Exécution: dotnet $($publishArgs -join ' ')" -ForegroundColor Gray
    #$process = Start-Process -FilePath "dotnet" -ArgumentList $publishArgs -Wait -PassThru -NoNewWindow 

    #return $process.ExitCode
}


# Script principal
try {
	$src='src/bg-nodus-broker'
    Write-Host "`n***  COMPILATION $src ($target)  ***`n" -ForegroundColor Blue

    # Vérification du dossier src
    if (-not (Test-Path $src)) {
        throw "Dossier $src introuvable"
    }

    # Se positionner dans le répertoire src
    Set-Location -Path $src -ErrorAction Stop

    # Nettoyage des dossiers bin et obj
    Write-Host "Nettoyage des dossiers temporaires ... " -NoNewline
    $foldersToRemove = @("bin", "obj", "dist")
    foreach ($folder in $foldersToRemove) {
        if (Test-Path $folder) {
            Remove-Item -Path $folder -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "✅" -ForegroundColor Green

    # Publication
    $isOnline = Test-NuGetConnection
    
    if ($isOnline) {
        Write-Host "EN LIGNE" -ForegroundColor Green -NoNewline
        Write-Host " - avec NuGet.org"
    } else {
        Write-Host "HORS LIGNE" -ForegroundColor Yellow -NoNewline
        Write-Host " - sources locales uniquement" 
        Write-Host "⚠️ Assurez-vous que votre environnement de développement est correctement initialisé" -ForegroundColor Yellow
    }

    $exitCode = Invoke-DotNetPublish -Target $target -Online $isOnline

    if ($exitCode -ne 0) {
        throw 
    }
    
    Write-Host "Publication ... " -NoNewline
    Write-Host "✅" -ForegroundColor Green

    # Retour au dossier parent
    Set-Location -Path ".."
    
    
    Write-Host "`n=== 👍 OPÉRATION TERMINÉ AVEC SUCCÈS 👍 ===" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "`n===  ÉCHEC  ===" -ForegroundColor Red
    Write-Host "Erreur: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    # Toujours restaurer le répertoire original
    Set-Location -Path $originalDirectory
}