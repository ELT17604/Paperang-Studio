import AppKit
import Foundation

enum AppIdentity {
 static let version = Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "0.2.3-fix1"
 static let copyright = "© 2026 ELT Group"
 static let statement = """
Paperang Studio is an independent, unofficial project. It is not affiliated with, endorsed by, sponsored by, or a subsidiary product of Paperang, its parent company, or any of their related companies or affiliates. All third-party names and trademarks belong to their respective owners.

Intended for personal research and learning. Commercial use is prohibited under the project's CC BY-NC-SA 4.0 license. When sharing this project or adaptations, credit ELT Group, retain the copyright and license notices, identify changes, and apply the required ShareAlike terms.

Provided as is, without warranty of any kind.
Contact: vox@calx.cc
"""
}
extension Lab {
 func showAbout(){
  let alert=NSAlert()
  alert.messageText="Paperang Studio \(AppIdentity.version)"
  alert.informativeText=AppIdentity.copyright+"\n\n"+AppIdentity.statement
  if let path=Bundle.main.path(forResource:"AppIcon",ofType:"icns"),let icon=NSImage(contentsOfFile:path){alert.icon=icon}
  alert.addButton(withTitle:"OK")
  alert.runModal()
 }
}
