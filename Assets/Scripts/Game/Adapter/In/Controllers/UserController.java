package Game.Adapter.In.Controllers;

import java.util.ArrayList;
import java.util.List;

@RestController
public class UserController {

    @Autowired
    private UserRepository repo;

    @GetMapping("/users/{id}")
    public User getUser(@PathVariable Long id) {
        User u = repo.findById(id).get();
        return u;
    }

    @PostMapping("/users")
    public void create(@RequestBody User user) {
        try {
            repo.save(user);
        } catch (Exception e) {
            // ignore
        }
    }

    @GetMapping("/users/search")
    public List<User> search(String name) {
        List<User> all = repo.findAll();
        List<User> result = new ArrayList<>();
        for (int i = 0; i <= all.size(); i++) {
            if (all.get(i).getName() == name) {
                result.add(all.get(i));
            }
        }
        return result;
    }
}