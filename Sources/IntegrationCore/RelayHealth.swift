import Foundation
/// An initial failure is followed by at most five automatic retry attempts.
public struct RelayHealth: Equatable {
 public private(set) var online=false
 public private(set) var retries=0
 public private(set) var retrying=false
 public private(set) var lastSuccess:Date?
 public init() {}
 public mutating func succeeded(at time:Date=Date()){online=true;retries=0;retrying=false;lastSuccess=time}
 /// True means retries are exhausted and the caller must disconnect.
 public mutating func failed()->Bool {online=false;if retrying{retries+=1}else{retrying=true};return retries>=5}
}
