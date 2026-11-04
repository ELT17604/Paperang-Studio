import SwiftUI
extension Lab {
 func manageRelayKeys(_ action:String="",id:String="",name:String="") {
  guard relayOnline,!relayAdminBusy,!relayAdminKey.isEmpty else{relayAdminMessage="请连接服务器并输入管理员密钥";return}
  relayAdminBusy=true
  Task{defer{self.relayAdminBusy=false};do{
   let credential=self.relayAdminKey.trimmingCharacters(in:.whitespacesAndNewlines)
   if action=="create"{let result=try await self.relayRequest("POST","v1/admin/keys",["name":self.relayKeyName,"role":self.relayNewRole,"device":self.relayKeyDevice],credential:credential);self.relayCreatedToken=result["token"] as? String ?? ""}
   else if !action.isEmpty{_ = try await self.relayRequest("POST","v1/admin/keys/\(id)/\(action)",["name":name],credential:credential)}
   let result=try await self.relayRequest("GET","v1/admin/keys",credential:credential)
   self.relayManagedKeys=(result["keys"] as? [[String:Any]] ?? []).compactMap{r in guard let id=r["id"] as? String,let name=r["name"] as? String,let role=r["role"] as? String,let device=r["device"] as? String else{return nil};return RelayManagedKey(id:id,name:name,role:role,device:device,revoked:(r["revoked"] as? Int ?? 0) != 0)}
   self.relayAdminMessage="已从服务器刷新 · \(self.relayManagedKeys.count) 个密钥"
  }catch{self.relayAdminMessage=error.localizedDescription}}
 }
}
extension Content {
 var relayKeyManager:some View {
  DisclosureGroup("用户密钥管理（管理员权限）") {
   VStack(alignment:.leading,spacing:10){
    SecureField("管理员密钥（仅保存在内存）",text:$lab.relayAdminKey).textFieldStyle(.roundedBorder)
    HStack{Button("刷新密钥"){lab.manageRelayKeys()};Text(lab.relayAdminMessage).font(.caption)}
    HStack{TextField("使用者展示名称",text:$lab.relayKeyName);TextField("目标设备",text:$lab.relayKeyDevice);Picker("权限",selection:$lab.relayNewRole){Text("投稿").tag("submit");Text("设备").tag("device");Text("管理员").tag("admin")};Button("新建密钥"){lab.manageRelayKeys("create")}.disabled(lab.relayKeyName.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty)}
    if !lab.relayCreatedToken.isEmpty{Text("新密钥仅展示本次，请妥善保存").font(.caption);SecureField("新密钥",text:$lab.relayCreatedToken);Button("复制新密钥"){NSPasteboard.general.clearContents();NSPasteboard.general.setString(lab.relayCreatedToken,forType:.string)};Button("隐藏新密钥"){lab.relayCreatedToken=""}}
    ForEach(lab.relayManagedKeys){key in
     HStack{TextField("展示名称",text:Binding(get:{lab.relayManagedKeys.first{$0.id==key.id}?.name ?? key.name},set:{value in if let i=lab.relayManagedKeys.firstIndex(where:{$0.id==key.id}){lab.relayManagedKeys[i].name=value}}));Text(key.role+" · "+key.device+(key.revoked ? " · 已吊销":" · 有效")).font(.caption)
      Button("保存名称"){lab.manageRelayKeys("rename",id:key.id,name:lab.relayManagedKeys.first{$0.id==key.id}?.name ?? key.name)}
      if key.revoked{Button("删除",role:.destructive){lab.manageRelayKeys("delete",id:key.id)}}else{Button("吊销",role:.destructive){lab.manageRelayKeys("revoke",id:key.id)}}
     }
    }
    Text("先吊销才能删除；名称更改用于后续投稿，历史任务保留当时身份。当前管理员密钥受保护，不能在此吊销或删除自身。").font(.caption).foregroundStyle(.secondary)
   }.disabled(!lab.relayOnline || lab.relayAdminBusy)
  }
 }
}
