using System.Reflection;
using System.Runtime.InteropServices;
using VRCModUpdater.Core;

// Les informations générales relatives à un assembly dépendent de
// l'ensemble d'attributs suivant. Changez les valeurs de ces attributs pour modifier les informations
// associées à un assembly.
[assembly: AssemblyTitle("VRCModUpdater.Core")]
[assembly: AssemblyDescription("Automatic mod updater plugin for VRChat, using MelonLoader")]
[assembly: AssemblyCompany("VRChat Modding Group")]
[assembly: AssemblyProduct("VRCModUpdater")]
[assembly: AssemblyCopyright("Copyright © Slaynash 2021")]

// L'affectation de la valeur false à ComVisible rend les types invisibles dans cet assembly
// aux composants COM. Si vous devez accéder à un type dans cet assembly à partir de
// COM, affectez la valeur true à l'attribut ComVisible sur ce type.
[assembly: ComVisible(false)]

// Le GUID suivant est pour l'ID de la typelib si ce projet est exposé à COM
[assembly: Guid("40b6b5c1-80a7-4714-9a61-b30b12e38776")]

// Les informations de version pour un assembly se composent des quatre valeurs suivantes :
//
//      Version principale
//      Version secondaire
//      Numéro de build
//      Révision
//
// Vous pouvez spécifier toutes les valeurs ou indiquer les numéros de build et de révision par défaut
// en utilisant '*', comme indiqué ci-dessous :
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion(VRCModUpdaterCore.VERSION)]
[assembly: AssemblyFileVersion(VRCModUpdaterCore.VERSION)]
