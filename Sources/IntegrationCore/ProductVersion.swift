import Foundation

/// Compare canonical Mac distribution tags, including historical opt/fix revisions.
public enum ProductVersion {
 public static func parse(_ tag:String)->[Int]? {
  guard tag.range(of:"^v?[0-9]+\\.[0-9]+\\.[0-9]+-macclient(?:-(?:fix|opt)[0-9]+)?$",options:.regularExpression) != nil else{return nil}
  let parts=tag.hasPrefix("v") ? String(tag.dropFirst()).split(separator:"-"):tag.split(separator:"-")
  var numbers=parts[0].split(separator:".").compactMap{Int($0)}
  guard numbers.count==3 else{return nil}
  if parts.count>2 {guard let revision=Int(parts[2].dropFirst(3)) else{return nil};numbers += [parts[2].hasPrefix("opt") ? 1:0,revision]}else{numbers += [0,0]}
  return numbers
 }
}
